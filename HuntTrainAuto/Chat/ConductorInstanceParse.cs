#nullable enable
using System;
using System.Text.RegularExpressions;

namespace HuntTrainAuto.Chat;

/// <summary>
/// Pure parse of instance numbers from conductor / Sonar-style chat text.
/// Prefers FFXIV instance glyphs (HTA <c>SonarMonitor.ParseInstanceNumber</c>);
/// falls back to plain <c>i1</c>–<c>i9</c> tokens conductors type beside flags.
/// </summary>
public static class ConductorInstanceParse
{
	/// <summary>FFXIV private-use instance markers 1–3 (U+E0B1..U+E0B3).</summary>
	public static readonly string[] InstanceGlyphs = ["\uE0B1", "\uE0B2", "\uE0B3"];

	private static readonly Regex AsciiInstance = new(
		@"\bi([1-9])\b",
		RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

	/// <summary>
	/// First matching instance in <paramref name="content"/>, or 0 when unspecified.
	/// Glyphs win over ASCII when both appear.
	/// </summary>
	public static int TryParse(string? content)
	{
		if (string.IsNullOrEmpty(content))
			return 0;

		for (var i = 0; i < InstanceGlyphs.Length; i++)
		{
			if (content.Contains(InstanceGlyphs[i], StringComparison.Ordinal))
				return i + 1;
		}

		var ascii = AsciiInstance.Match(content);
		if (ascii.Success
			&& int.TryParse(ascii.Groups[1].Value, out var n)
			&& n is >= 1 and <= 9)
			return n;

		return 0;
	}
}
