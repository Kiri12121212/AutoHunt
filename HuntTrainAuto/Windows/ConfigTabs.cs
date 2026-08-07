#nullable enable
using System;

namespace HuntTrainAuto.Windows;

/// <summary>
/// Pure helpers for config-window tab ids, mount labels, and TP-delay clamps (TASKS 8.1–8.6).
/// </summary>
public static class ConfigTabs
{
	public const int Status = 0;
	public const int Settings = 1;
	public const int Mount = 2;
	public const int Follow = 3;
	public const int Combat = 4;
	public const int Integrations = 5;
	public const int Debug = 6;

	public static readonly string[] Labels =
	[
		"Status",
		"Settings",
		"Mount",
		"Follow",
		"Combat",
		"Integrations",
		"Debug",
	];

	public const string HuntAlertsDisplayName = "HuntAlerts";
	public const string HuntAlertsPlaceholderStatus = "Phase 10 — not wired";

	public const int MinTeleportDelayMs = 0;
	public const int MaxTeleportDelayMs = 30_000;

	public const float MinAutoTeleportSkipDistance = 0f;
	public const float MaxAutoTeleportSkipDistance = 50f;
	public const float DefaultAutoTeleportSkipDistance = 3f;

	public const float MinFlagArrivalTolerance = 1f;
	public const float MaxFlagArrivalTolerance = 25f;

	/// <summary>Clamp tab index into <see cref="Labels"/> range.</summary>
	public static int ClampSelected(int selected)
	{
		if (selected < 0)
			return Status;
		if (selected >= Labels.Length)
			return Labels.Length - 1;
		return selected;
	}

	public static string LabelAt(int index)
		=> Labels[ClampSelected(index)];

	/// <summary>
	/// Human-readable mount selection: <c>-1</c> never, <c>0</c> random, else RowId.
	/// </summary>
	public static string FormatMountSelection(int mount)
		=> mount switch
		{
			MountDecision.NeverMount => "Never",
			MountDecision.RandomMount => "Random",
			_ => $"Mount #{mount}",
		};

	/// <summary>Normalize configured mount id (no upper bound — Excel RowIds vary).</summary>
	public static int ClampMountId(int mount)
		=> mount < MountDecision.NeverMount ? MountDecision.NeverMount : mount;

	public static string FormatHuntAlertsPlaceholder()
		=> $"{HuntAlertsDisplayName}: {HuntAlertsPlaceholderStatus}";

	/// <summary>
	/// Clamp a single TP delay endpoint; NaN-safe via int (already finite).
	/// </summary>
	public static int ClampTeleportDelayMs(int ms)
	{
		if (ms < MinTeleportDelayMs)
			return MinTeleportDelayMs;
		if (ms > MaxTeleportDelayMs)
			return MaxTeleportDelayMs;
		return ms;
	}

	/// <summary>
	/// Clamp min/max TP delay and ensure <paramref name="minMs"/> ≤ <paramref name="maxMs"/>.
	/// </summary>
	public static (int MinMs, int MaxMs) ClampTeleportDelayRange(int minMs, int maxMs)
	{
		var min = ClampTeleportDelayMs(minMs);
		var max = ClampTeleportDelayMs(maxMs);
		if (max < min)
			(min, max) = (max, min);
		return (min, max);
	}

	public static float ClampAutoTeleportSkipDistance(float distance)
	{
		if (float.IsNaN(distance) || float.IsInfinity(distance))
			return DefaultAutoTeleportSkipDistance;
		if (distance < MinAutoTeleportSkipDistance)
			return MinAutoTeleportSkipDistance;
		if (distance > MaxAutoTeleportSkipDistance)
			return MaxAutoTeleportSkipDistance;
		return distance;
	}

	public static float ClampFlagArrivalTolerance(float tolerance)
	{
		if (float.IsNaN(tolerance) || float.IsInfinity(tolerance))
			return FlagArrival.DefaultTolerance;
		if (tolerance < MinFlagArrivalTolerance)
			return MinFlagArrivalTolerance;
		if (tolerance > MaxFlagArrivalTolerance)
			return MaxFlagArrivalTolerance;
		return tolerance;
	}
}
