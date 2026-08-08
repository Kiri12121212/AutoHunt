#nullable enable
using System;
using System.Text.RegularExpressions;

namespace HuntTrainAuto.Chat;

/// <summary>
/// Pure parse of conductor "LAST STOP" / end-of-train chat macros.
/// </summary>
public static class ConductorLastStopParse
{
	private static readonly Regex LastStop = new(
		@"\blast\s*stop\b",
		RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

	/// <summary>True when <paramref name="content"/> announces the final train stop.</summary>
	public static bool IsLastStop(string? content)
	{
		if (string.IsNullOrWhiteSpace(content))
			return false;
		return LastStop.IsMatch(content);
	}
}
