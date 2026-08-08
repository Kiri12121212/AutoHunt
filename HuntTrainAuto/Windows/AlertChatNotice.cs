#nullable enable

using System;
using HuntTrainAuto.HuntAlerts;

namespace HuntTrainAuto.Windows;

/// <summary>
/// Pure HuntAlerts-style chat notice text (unit-testable without Dalamud).
/// </summary>
public static class AlertChatNotice
{
	public const string ClickHint = "(Click for info)";

	/// <summary>HuntAlerts train chat foreground (config.TextColor default).</summary>
	public const ushort TrainUiForeground = 57;

	/// <summary>HuntAlerts S-rank chat foreground (config.SRankTextColor default).</summary>
	public const ushort SRankUiForeground = 48;

	public static ushort UiForegroundFor(HuntTrainMessage message)
		=> AlertInfoDisplay.IsTrain(message) ? TrainUiForeground : SRankUiForeground;

	/// <summary>
	/// e.g. <c>Dawntrail train starting on Shiva! (Click for info)</c>
	/// or <c>Dawntrail S Rank Ker spawned on Shiva! (Click for info)</c>.
	/// </summary>
	public static string FormatLine(HuntTrainMessage message)
	{
		ArgumentNullException.ThrowIfNull(message);

		var kind = CleanOrFallback(message.huntKind, "Hunt");
		var world = Clean(message.huntWorld);

		if (AlertInfoDisplay.IsTrain(message))
		{
			var train = $"{kind} train starting";
			return string.IsNullOrEmpty(world)
				? $"{train}! {ClickHint}"
				: $"{train} on {world}! {ClickHint}";
		}

		var creature = Clean(message.creatureName);
		var inst = message.instance > 1 ? $" (i{message.instance})" : "";
		var core = string.IsNullOrEmpty(creature)
			? $"{kind} S Rank{inst} spawned"
			: $"{kind} S Rank {creature}{inst} spawned";
		return string.IsNullOrEmpty(world)
			? $"{core}! {ClickHint}"
			: $"{core} on {world}! {ClickHint}";
	}

	private static string Clean(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return "";
		var t = value.Trim();
		if (t.Equals("invalid", StringComparison.OrdinalIgnoreCase))
			return "";
		if (t.Equals("unknown", StringComparison.OrdinalIgnoreCase))
			return "";
		return t;
	}

	private static string CleanOrFallback(string? value, string fallback)
	{
		var c = Clean(value);
		return string.IsNullOrEmpty(c) ? fallback : c;
	}
}
