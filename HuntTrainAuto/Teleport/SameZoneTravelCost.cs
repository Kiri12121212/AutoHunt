#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;

namespace HuntTrainAuto.Teleport;

/// <summary>Whether same-zone vnav path lengths are usable for time-aware TP.</summary>
public enum SameZonePathCostStatus
{
	/// <summary>vnav unavailable, pathfind failed, or endpoints missing — use distance fallback.</summary>
	Unavailable,

	/// <summary>Pathfind in flight — defer same-zone far TP until Ready or Unavailable.</summary>
	Pending,

	/// <summary>Both path lengths available for time comparison.</summary>
	Ready,
}

/// <summary>
/// Injected path lengths for same-zone time-aware teleport (no live vnav in unit tests).
/// </summary>
public readonly struct SameZoneTravelEstimate
{
	public required SameZonePathCostStatus Status { get; init; }

	/// <summary>Mesh path length player → flag (yalms); set when <see cref="Status"/> is Ready.</summary>
	public float? DirectPathLengthYalms { get; init; }

	/// <summary>Mesh path length aetheryte → flag (yalms); set when <see cref="Status"/> is Ready.</summary>
	public float? AetherytePathLengthYalms { get; init; }

	/// <summary>Compact, side-effect-free estimate diagnostic for call-site logging.</summary>
	public string Describe()
		=> $"status={Status}, direct={DirectPathLengthYalms?.ToString() ?? "none"}, "
			+ $"aetheryte={AetherytePathLengthYalms?.ToString() ?? "none"}";
}

/// <summary>Config constants for same-zone time-aware TP (pure; mirrored on <see cref="Configuration"/>).</summary>
public readonly struct SameZoneTimeAwareSettings
{
	public bool Enabled { get; init; }

	/// <summary>Random pre-delay before cast, in seconds (0 when delay disabled).</summary>
	public float PreDelaySeconds { get; init; }

	/// <summary>Teleport cast duration (~Action 5).</summary>
	public float CastSeconds { get; init; }

	/// <summary>Configurable BetweenAreas load estimate (seconds).</summary>
	public float LoadEstimateSeconds { get; init; }

	/// <summary>Assumed mount travel speed (yalms / second).</summary>
	public float MountSpeedYalmsPerSec { get; init; }

	/// <summary>Optional mount-up overhead added to ride legs (seconds).</summary>
	public float MountUpSeconds { get; init; }

	/// <summary>
	/// When true, distance ≤ threshold still skips TP (AlreadyClose) before time compare.
	/// Distance remains the soft-fallback when path costs are unavailable.
	/// </summary>
	public bool RetainDistanceAsFloor { get; init; }

	public static SameZoneTimeAwareSettings Disabled { get; } = new()
	{
		Enabled = false,
		PreDelaySeconds = 0f,
		CastSeconds = SameZoneTravelCost.DefaultCastSeconds,
		LoadEstimateSeconds = SameZoneTravelCost.DefaultLoadEstimateSeconds,
		MountSpeedYalmsPerSec = SameZoneTravelCost.DefaultMountSpeedYalmsPerSec,
		MountUpSeconds = SameZoneTravelCost.DefaultMountUpSeconds,
		RetainDistanceAsFloor = true,
	};
}

/// <summary>
/// Pure same-zone travel-time helpers (vnav path lengths injected; no IPC).
/// </summary>
public static class SameZoneTravelCost
{
	/// <summary>Compact, side-effect-free time-comparison diagnostic for call-site logging.</summary>
	public static string DescribeDecision(bool? skipTeleport)
		=> skipTeleport switch
		{
			true => "time decision=skip teleport",
			false => "time decision=teleport",
			_ => "time decision=fallback",
		};

	public const float DefaultMountSpeedYalmsPerSec = 20f;
	public const float DefaultCastSeconds = 5f;
	public const float DefaultLoadEstimateSeconds = 8f;
	public const float DefaultMountUpSeconds = 1.5f;

	/// <summary>Sum consecutive waypoint segment lengths; 0 when fewer than 2 points.</summary>
	public static float PathLength(IReadOnlyList<Vector3> waypoints)
	{
		ArgumentNullException.ThrowIfNull(waypoints);
		if (waypoints.Count < 2)
			return 0f;

		var sum = 0f;
		for (var i = 1; i < waypoints.Count; i++)
			sum += Vector3.Distance(waypoints[i - 1], waypoints[i]);
		return sum;
	}

	/// <summary>Ride time = path / speed (+ optional mount-up). Null speed or non-positive → null.</summary>
	public static float? RideSeconds(float pathLengthYalms, float mountSpeedYalmsPerSec, float mountUpSeconds = 0f)
	{
		if (mountSpeedYalmsPerSec <= 0f || float.IsNaN(mountSpeedYalmsPerSec) || float.IsInfinity(mountSpeedYalmsPerSec))
			return null;
		if (float.IsNaN(pathLengthYalms) || float.IsInfinity(pathLengthYalms) || pathLengthYalms < 0f)
			return null;

		var mountUp = mountUpSeconds > 0f && !float.IsNaN(mountUpSeconds) && !float.IsInfinity(mountUpSeconds)
			? mountUpSeconds
			: 0f;
		return (pathLengthYalms / mountSpeedYalmsPerSec) + mountUp;
	}

	/// <summary>
	/// t_tp = pre-delay + cast + load + ride(aetheryte → flag).
	/// </summary>
	public static float? TeleportTotalSeconds(
		float preDelaySeconds,
		float castSeconds,
		float loadEstimateSeconds,
		float aetherytePathLengthYalms,
		float mountSpeedYalmsPerSec,
		float mountUpSeconds = 0f)
	{
		var ride = RideSeconds(aetherytePathLengthYalms, mountSpeedYalmsPerSec, mountUpSeconds);
		if (ride == null)
			return null;

		return SafeNonNegative(preDelaySeconds)
			+ SafeNonNegative(castSeconds)
			+ SafeNonNegative(loadEstimateSeconds)
			+ ride.Value;
	}

	/// <summary>Skip TP when direct ride is no slower than TP overhead + aetheryte ride.</summary>
	public static bool PreferDirect(float tDirectSeconds, float tTeleportSeconds)
		=> tDirectSeconds <= tTeleportSeconds;

	/// <summary>
	/// Path-cost Unavailable soft-fall: skip aetheryte TP when already within the yalm
	/// floor, or when the player is no farther from the flag than the aetheryte
	/// (mid-air / off-mesh pathfind must not TP you farther away).
	/// </summary>
	public static bool ShouldSkipTeleportWhenPathCostUnavailable(
		float? playerDistanceYalms,
		float? aetheryteDistanceYalms,
		float distanceThreshold)
	{
		if (playerDistanceYalms is not { } pd
		    || float.IsNaN(pd)
		    || float.IsInfinity(pd)
		    || pd < 0f)
			return false;

		if (pd <= distanceThreshold)
			return true;

		if (aetheryteDistanceYalms is not { } ad
		    || float.IsNaN(ad)
		    || float.IsInfinity(ad)
		    || ad < 0f)
			return false;

		return pd <= ad;
	}

	/// <summary>
	/// Same-zone far decision from injected lengths.
	/// Returns null when estimate incomplete / invalid — caller soft-falls to distance threshold.
	/// True = skip TP (direct faster); false = TeleportBecauseFar.
	/// </summary>
	public static bool? ShouldSkipTeleportForTime(
		SameZoneTravelEstimate estimate,
		SameZoneTimeAwareSettings settings)
	{
		if (!settings.Enabled || estimate.Status != SameZonePathCostStatus.Ready)
			return null;

		if (estimate.DirectPathLengthYalms is not { } direct
			|| estimate.AetherytePathLengthYalms is not { } fromAeth)
			return null;

		var tDirect = RideSeconds(direct, settings.MountSpeedYalmsPerSec, settings.MountUpSeconds);
		var tTp = TeleportTotalSeconds(
			settings.PreDelaySeconds,
			settings.CastSeconds,
			settings.LoadEstimateSeconds,
			fromAeth,
			settings.MountSpeedYalmsPerSec,
			settings.MountUpSeconds);
		if (tDirect == null || tTp == null)
			return null;

		return PreferDirect(tDirect.Value, tTp.Value);
	}

	/// <summary>Average pre-delay in seconds from ms range (0 when disabled).</summary>
	public static float PreDelaySecondsFromMs(bool delayEnabled, int delayMinMs, int delayMaxMs)
	{
		if (!delayEnabled)
			return 0f;

		var min = Math.Max(0, delayMinMs);
		var max = Math.Max(min, delayMaxMs);
		return ((min + max) * 0.5f) / 1000f;
	}

	/// <summary>Build settings from config fields (pure). Time-aware compare is always enabled.</summary>
	public static SameZoneTimeAwareSettings CreateSettings(
		float castSeconds,
		float loadEstimateSeconds,
		float mountSpeedYalmsPerSec,
		float mountUpSeconds,
		bool retainDistanceAsFloor,
		bool teleportDelayEnabled,
		int teleportDelayMinMs,
		int teleportDelayMaxMs)
		=> new()
		{
			Enabled = true,
			PreDelaySeconds = PreDelaySecondsFromMs(
				teleportDelayEnabled,
				teleportDelayMinMs,
				teleportDelayMaxMs),
			CastSeconds = castSeconds >= 0f ? castSeconds : DefaultCastSeconds,
			LoadEstimateSeconds = loadEstimateSeconds >= 0f ? loadEstimateSeconds : DefaultLoadEstimateSeconds,
			MountSpeedYalmsPerSec = mountSpeedYalmsPerSec > 0f
				? mountSpeedYalmsPerSec
				: DefaultMountSpeedYalmsPerSec,
			MountUpSeconds = mountUpSeconds >= 0f ? mountUpSeconds : DefaultMountUpSeconds,
			RetainDistanceAsFloor = retainDistanceAsFloor,
		};

	private static float SafeNonNegative(float value)
	{
		if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
			return 0f;
		return value;
	}

}
