#nullable enable

using System;
using System.Globalization;
using HuntTrainAuto.HuntAlerts;

namespace HuntTrainAuto.Windows;

/// <summary>
/// Pure formatters for HuntAlerts-style alert info (unit-testable without ImGui).
/// </summary>
public static class AlertInfoDisplay
{
	public const string EmptyLabel = "(none)";
	public const string KindTrain = "Hunt Train";
	public const string KindSRank = "S Rank";

	public static bool IsTrain(HuntTrainMessage? message)
		=> message != null
		   && string.Equals(message.huntType, HuntAlertsFilter.HuntTypeATrain, StringComparison.OrdinalIgnoreCase);

	public static string FormatBadge(HuntTrainMessage message)
		=> IsTrain(message) ? "TRAIN" : "S RANK";

	public static string FormatTitle(HuntTrainMessage message)
	{
		var kind = CleanOrFallback(message.huntKind, "Hunt");
		return IsTrain(message) ? $"{kind} train" : $"{kind} S Rank";
	}

	public static string FormatKind(HuntTrainMessage message)
		=> IsTrain(message) ? KindTrain : KindSRank;

	public static string FormatHunt(HuntTrainMessage message)
		=> CleanOrFallback(message.huntKind, EmptyLabel);

	public static string FormatStartZone(HuntTrainMessage message)
		=> CleanOrFallback(message.startZone, EmptyLabel);

	public static string FormatAetheryte(HuntTrainMessage message)
		=> CleanOrFallback(message.startLocation, EmptyLabel);

	public static string FormatWorld(HuntTrainMessage message)
		=> CleanOrFallback(message.huntWorld, EmptyLabel);

	public static string FormatCreature(HuntTrainMessage message)
		=> CleanOrFallback(message.creatureName, EmptyLabel);

	public static string FormatPosted(HuntTrainMessage message)
	{
		if (!string.IsNullOrWhiteSpace(message.Posted_Time))
			return message.Posted_Time.Trim();
		if (message.PostedEpoch > 0)
		{
			try
			{
				return DateTimeOffset.FromUnixTimeSeconds(message.PostedEpoch)
					.ToLocalTime()
					.ToString("h:mm tt", CultureInfo.InvariantCulture);
			}
			catch
			{
				// soft-fail to empty
			}
		}

		return EmptyLabel;
	}

	public static string FormatStatusSummary(HuntTrainMessage? message)
	{
		if (message == null)
			return $"Last HuntAlerts: {EmptyLabel}";

		var title = FormatTitle(message);
		var world = Clean(message.huntWorld);
		var zone = Clean(message.startZone);
		var where = !string.IsNullOrEmpty(world) && !string.IsNullOrEmpty(zone)
			? $"{world} / {zone}"
			: !string.IsNullOrEmpty(world)
				? world
				: !string.IsNullOrEmpty(zone)
					? zone
					: null;
		return where == null ? $"Last HuntAlerts: {title}" : $"Last HuntAlerts: {title} · {where}";
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
