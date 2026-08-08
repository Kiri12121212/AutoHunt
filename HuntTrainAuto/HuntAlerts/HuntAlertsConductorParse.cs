#nullable enable

using System;
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
	/// </summary>
	[GeneratedRegex(
		@"\bConductor\b[\t *_]*[:\-–—][\t *_]*[ \t]*(?:\[(?<world>[^\]]+)\][ \t]*)?(?<name>[A-Za-z][A-Za-z'\-]{0,14}(?:[ \t]+[A-Za-z][A-Za-z'\-]{0,14}){1,2})(?:[ \t]*@[ \t]*(?<world2>[A-Za-z][A-Za-z'\-]{0,19}))?",
		RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	private static partial Regex ConductorRegex();

	/// <summary>
	/// Try to pull a player name (and optional home world) from HA message text.
	/// Returns bare character name suitable for <see cref="Chat.ConductorList.TryAdd"/>.
	/// </summary>
	public static bool TryExtract(string? message, out string name, out string? world)
	{
		name = null!;
		world = null;

		if (string.IsNullOrWhiteSpace(message))
			return false;

		var match = ConductorRegex().Match(message);
		if (!match.Success)
			return false;

		var rawName = match.Groups["name"].Value.Trim();
		if (string.IsNullOrEmpty(rawName))
			return false;

		// Strip leftover markdown emphasis around the capture.
		rawName = rawName.Trim('*', '_', '`');
		if (string.IsNullOrEmpty(rawName))
			return false;

		var worldGroup = match.Groups["world"];
		var world2Group = match.Groups["world2"];
		string? rawWorld = null;
		if (worldGroup.Success && !string.IsNullOrWhiteSpace(worldGroup.Value))
			rawWorld = worldGroup.Value.Trim().Trim('*', '_', '`');
		else if (world2Group.Success && !string.IsNullOrWhiteSpace(world2Group.Value))
			rawWorld = world2Group.Value.Trim().Trim('*', '_', '`');

		if (string.IsNullOrEmpty(rawWorld))
			rawWorld = null;

		name = rawName;
		world = rawWorld;
		return true;
	}

	/// <summary>Convenience overload when world is unused.</summary>
	public static bool TryExtract(string? message, out string name)
		=> TryExtract(message, out name, out _);
}
