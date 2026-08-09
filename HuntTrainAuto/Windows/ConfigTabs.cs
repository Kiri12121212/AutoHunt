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
	public const int Engage = 3;
	public const int Combat = 4;
	public const int Integrations = 5;
	public const int Debug = 6;

	public static readonly string[] Labels =
	[
		"Status",
		"Settings",
		"Mount",
		"Engage",
		"Combat",
		"Integrations",
		"Debug",
	];

	public const int MinTeleportDelayMs = 0;
	public const int MaxTeleportDelayMs = 30_000;

	public const float MinAutoTeleportSkipDistance = 0f;
	public const float MaxAutoTeleportSkipDistance = 500f;
	public const float DefaultAutoTeleportSkipDistance = 150f;

	/// <summary>
	/// Config <c>Version</c> once <see cref="Configuration.AutoTeleportAetheryteDistanceDiff"/>
	/// is stored in yalms (was map-coordinate units; default 3 ≈ 150 yalms @ sizeFactor 100).
	/// </summary>
	public const int YalmSkipDistanceConfigVersion = 2;

	/// <summary>
	/// Config <c>Version</c> once RSR defaults prefer hunt A-rank
	/// (<c>TargetsHaveTarget</c> + <c>HighMaxHP</c>) instead of AutoDuty trash pulls.
	/// </summary>
	public const int HuntRsrTargetingConfigVersion = 3;

	/// <summary>Legacy map-units → yalms (3 → 150).</summary>
	public const float LegacyMapUnitToYalmFactor = 50f;

	public const float MinFlagArrivalTolerance = 3f;
	public const float MaxFlagArrivalTolerance = 25f;

	public const float MinTeleportCastSeconds = 0f;
	public const float MaxTeleportCastSeconds = 30f;
	public const float MinTeleportLoadEstimateSeconds = 0f;
	public const float MaxTeleportLoadEstimateSeconds = 60f;
	public const float MinMountSpeedYalmsPerSec = 1f;
	public const float MaxMountSpeedYalmsPerSec = 80f;
	public const float MinMountUpSeconds = 0f;
	public const float MaxMountUpSeconds = 15f;

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

	/// <summary>
	/// Convert a pre-yalm (map-coordinate) skip distance to yalms and clamp.
	/// </summary>
	public static float ScaleLegacyMapSkipDistanceToYalms(float legacyMapUnits)
		=> ClampAutoTeleportSkipDistance(legacyMapUnits * LegacyMapUnitToYalmFactor);

	/// <summary>True when persisted config still stores map-coordinate skip distances.</summary>
	public static bool NeedsYalmSkipDistanceMigration(int configVersion)
		=> configVersion < YalmSkipDistanceConfigVersion;

	/// <summary>True when RSR settings still need hunt A-rank targeting migration.</summary>
	public static bool NeedsHuntRsrTargetingMigration(int configVersion)
		=> configVersion < HuntRsrTargetingConfigVersion;

	/// <summary>
	/// Upgrade AutoDuty-style RSR settings to hunt defaults when still on stock AD values.
	/// Custom user choices are left alone. Returns true when any value changed.
	/// </summary>
	public static bool TryMigrateHuntRsrTargeting(
		ref RsrTargetHostileType hostileType,
		ref RsrTargetingType targetingTank,
		ref RsrTargetingType targetingNonTank)
	{
		var changed = false;
		if (hostileType == RsrTargetHostileType.AllTargetsCanAttack)
		{
			hostileType = RsrSettingsDecision.DefaultHostileType;
			changed = true;
		}

		if (targetingTank is RsrTargetingType.HighHP or RsrTargetingType.LowHP)
		{
			targetingTank = RsrSettingsDecision.DefaultTankTargeting;
			changed = true;
		}

		if (targetingNonTank is RsrTargetingType.HighHP or RsrTargetingType.LowHP)
		{
			targetingNonTank = RsrSettingsDecision.DefaultNonTankTargeting;
			changed = true;
		}

		return changed;
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

	public static float ClampTeleportCastSeconds(float seconds)
		=> ClampFinite(seconds, MinTeleportCastSeconds, MaxTeleportCastSeconds, SameZoneTravelCost.DefaultCastSeconds);

	public static float ClampTeleportLoadEstimateSeconds(float seconds)
		=> ClampFinite(
			seconds,
			MinTeleportLoadEstimateSeconds,
			MaxTeleportLoadEstimateSeconds,
			SameZoneTravelCost.DefaultLoadEstimateSeconds);

	public static float ClampMountSpeedYalmsPerSec(float speed)
		=> ClampFinite(
			speed,
			MinMountSpeedYalmsPerSec,
			MaxMountSpeedYalmsPerSec,
			SameZoneTravelCost.DefaultMountSpeedYalmsPerSec);

	public static float ClampMountUpSeconds(float seconds)
		=> ClampFinite(seconds, MinMountUpSeconds, MaxMountUpSeconds, SameZoneTravelCost.DefaultMountUpSeconds);

	private static float ClampFinite(float value, float min, float max, float fallback)
	{
		if (float.IsNaN(value) || float.IsInfinity(value))
			return fallback;
		if (value < min)
			return min;
		if (value > max)
			return max;
		return value;
	}
}
