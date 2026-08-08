#nullable enable

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace HuntTrainAuto.HuntAlerts;

/// <summary>
/// Pure extraction of a conductor identity from HuntAlerts train
/// <see cref="HuntTrainMessage.Message"/> free text (Discord content appended
/// after the structured header). Community formats vary; matches common
/// <c>Conductor - Name</c> / <c>Conductor: [World] Name</c> lines from live HA history.
/// </summary>
public static partial class HuntAlertsConductorParse
{
	/// <summary>
	/// FFXIV character names are First Last (letters, apostrophe, hyphen).
	/// Optional <c>[World]</c> or trailing <c>@World</c>.
	/// Negative lookbehind skips <c>Former</c>/<c>Not</c>/<c>Ex</c> Conductor phrases.
	/// </summary>
	[GeneratedRegex(
		@"(?<!(?i:Not|Former|Ex)\s)\bConductor\b[\t *_]*[:\-–—][\t *_]*[ \t]*(?:\[(?<world>[^\]]+)\][ \t]*)?(?<name>[A-Za-z][A-Za-z'\-]{0,14}(?:[ \t]+[A-Za-z][A-Za-z'\-]{0,14}){1,2})(?:[ \t]*@[ \t]*(?<world2>[A-Za-z][A-Za-z'\-]{0,19}))?",
		RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	private static partial Regex ConductorRegex();

	/// <summary>
	/// Phrase tails that match the name shape but are train-description noise, not players.
	/// </summary>
	private static readonly HashSet<string> NonNamePhrases = new(StringComparer.OrdinalIgnoreCase)
	{
		"Very Fast",
		"Always Bullet",
		"Bullet Train",
		"Full Speed",
		"Super Fast",
	};

	/// <summary>
	/// Try to pull a player name (and optional home world) from HA message text.
	/// Returns bare character name suitable for <see cref="Chat.ConductorList.TryAdd"/>.
	/// Prefers line-start / parenthetical conductor labels when multiple matches exist.
	/// </summary>
	public static bool TryExtract(string? message, out string name, out string? world)
	{
		name = null!;
		world = null;

		if (string.IsNullOrWhiteSpace(message))
			return false;

		Match? best = null;
		var bestScore = int.MinValue;
		foreach (Match match in ConductorRegex().Matches(message))
		{
			if (!match.Success)
				continue;

			var rawName = match.Groups["name"].Value.Trim().Trim('*', '_', '`');
			if (string.IsNullOrEmpty(rawName) || NonNamePhrases.Contains(rawName))
				continue;

			var score = ScoreMatch(message, match);
			if (best is null || score > bestScore || (score == bestScore && match.Index >= best.Index))
			{
				best = match;
				bestScore = score;
			}
		}

		if (best is null)
			return false;

		var chosenName = best.Groups["name"].Value.Trim().Trim('*', '_', '`');
		if (string.IsNullOrEmpty(chosenName))
			return false;

		var worldGroup = best.Groups["world"];
		var world2Group = best.Groups["world2"];
		string? rawWorld = null;
		if (worldGroup.Success && !string.IsNullOrWhiteSpace(worldGroup.Value))
			rawWorld = worldGroup.Value.Trim().Trim('*', '_', '`');
		else if (world2Group.Success && !string.IsNullOrWhiteSpace(world2Group.Value))
			rawWorld = world2Group.Value.Trim().Trim('*', '_', '`');

		if (string.IsNullOrEmpty(rawWorld))
			rawWorld = null;

		name = chosenName;
		world = rawWorld;
		return true;
	}

	/// <summary>Convenience overload when world is unused.</summary>
	public static bool TryExtract(string? message, out string name)
		=> TryExtract(message, out name, out _);

	private static int ScoreMatch(string message, Match match)
	{
		var score = 0;
		var idx = match.Index;
		// Line-start (or after whitespace-only prefix on the line) is the usual HA format.
		if (idx == 0 || message[idx - 1] == '\n' || message[idx - 1] == '\r')
			score += 20;
		else
		{
			var lineStart = message.LastIndexOf('\n', idx - 1) + 1;
			var prefix = message.AsSpan(lineStart, idx - lineStart).Trim();
			if (prefix.IsEmpty || prefix is ['*', ..] or ['_', ..] or ['(', ..] or ['[', ..])
				score += 15;
		}

		// Parenthetical (Conductor: …) from live HA history.
		if (idx > 0 && message[idx - 1] == '(')
			score += 10;

		return score;
	}
}
