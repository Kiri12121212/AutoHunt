#nullable enable
using System;
using System.Collections.Generic;

namespace HuntTrainAuto.Chat;

/// <summary>
/// Pure chat-sender decode and conductor name match (no Dalamud types).
/// </summary>
public static class ChatSender
{
	/// <summary>
	/// Prefer the first non-empty player-payload name; otherwise carefully fall back to
	/// plain text (trim, strip trailing <c>@World</c>).
	/// </summary>
	public static bool TryDecode(
		IEnumerable<string?> playerPayloadNames,
		string? textValue,
		out string playerName)
	{
		ArgumentNullException.ThrowIfNull(playerPayloadNames);

		foreach (var candidate in playerPayloadNames)
		{
			var trimmed = candidate?.Trim();
			if (!string.IsNullOrEmpty(trimmed))
			{
				playerName = trimmed;
				return true;
			}
		}

		if (!TryNormalizeTextFallback(textValue, out playerName))
			return false;

		return true;
	}

	public static bool IsConductor(IList<string> conductors, string playerName)
	{
		ArgumentNullException.ThrowIfNull(conductors);

		// Same trim + @World strip as text-fallback decode, on both sides.
		if (!TryNormalizeTextFallback(playerName, out var normalizedPlayer))
			return false;

		for (var i = 0; i < conductors.Count; i++)
		{
			if (!TryNormalizeTextFallback(conductors[i], out var normalizedConductor))
				continue;

			if (string.Equals(normalizedConductor, normalizedPlayer, StringComparison.OrdinalIgnoreCase))
				return true;
		}

		return false;
	}

	private static bool TryNormalizeTextFallback(string? textValue, out string playerName)
	{
		var text = textValue?.Trim();
		if (string.IsNullOrEmpty(text))
		{
			playerName = null!;
			return false;
		}

		var at = text.IndexOf('@');
		if (at >= 0)
			text = text[..at].TrimEnd();

		if (string.IsNullOrEmpty(text))
		{
			playerName = null!;
			return false;
		}

		playerName = text;
		return true;
	}
}
