#nullable enable
using System;

namespace HuntTrainAuto.Windows;

/// <summary>
/// Read-only runtime signals for the Config Status panel (TASKS 8.6).
/// Filled by Plugin; UI formats via <see cref="StatusDisplay"/> without game logic.
/// </summary>
public readonly struct StatusSnapshot
{
	public HuntTrainPhase Phase { get; init; }

	public bool Mounted { get; init; }

	public MountPhase MountPipeline { get; init; }

	public UnmountPhase UnmountPipeline { get; init; }

	public bool NavPathRunning { get; init; }

	public int NavWaypoints { get; init; }

	public bool NavPathfindInProgress { get; init; }

	/// <summary>True when Debug Fake Hunt session is armed.</summary>
	public bool FakeHuntActive { get; init; }

	/// <summary>Synthetic NearbyARank WorldPos is set.</summary>
	public bool FakeARankSet { get; init; }

	/// <summary>Short Fake Hunt summary for Status (empty when inactive).</summary>
	public string? FakeHuntSummary { get; init; }

	/// <summary>BossMod or BossModReborn loaded (preferred provider).</summary>
	public bool BossModAvailable { get; init; }

	/// <summary>Display name of active BM provider when available.</summary>
	public string? BossModProviderName { get; init; }

	/// <summary>True after HTA successfully enabled BM AI this combat latch.</summary>
	public bool BossModAiActive { get; init; }
}

/// <summary>
/// Pure formatters for Status panel lines (unit-testable without ImGui).
/// </summary>
public static class StatusDisplay
{
	public static string FormatPhase(HuntTrainPhase phase)
		=> phase switch
		{
			HuntTrainPhase.Idle => "Idle",
			HuntTrainPhase.Teleport => "Teleport",
			HuntTrainPhase.Mount => "Mount",
			HuntTrainPhase.Navigate => "Navigate",
			HuntTrainPhase.Unmount => "Unmount",
			HuntTrainPhase.Combat => "Combat",
			_ => phase.ToString(),
		};

	/// <summary>
	/// Prefer active mount/unmount pipeline labels; otherwise mounted / not mounted.
	/// </summary>
	public static string FormatMountStatus(
		bool mounted,
		MountPhase mountPipeline,
		UnmountPhase unmountPipeline)
	{
		if (unmountPipeline == UnmountPhase.Unmounting)
			return "Unmounting";
		if (unmountPipeline == UnmountPhase.WaitReady)
			return "Waiting to unmount";
		if (mountPipeline == MountPhase.Mounting)
			return "Mounting";
		if (mountPipeline == MountPhase.WaitReady)
			return "Waiting to mount";
		return mounted ? "Mounted" : "Not mounted";
	}

	public static string FormatNavProgress(bool pathRunning, int waypoints, bool pathfindInProgress)
	{
		if (pathfindInProgress)
			return "Pathfinding…";
		if (pathRunning)
		{
			if (waypoints > 0)
				return $"Navigating ({waypoints} waypoints)";
			return "Navigating";
		}

		return "Idle";
	}

	public static string FormatPhaseLine(HuntTrainPhase phase)
		=> $"State: {FormatPhase(phase)}";

	public static string FormatMountLine(
		bool mounted,
		MountPhase mountPipeline,
		UnmountPhase unmountPipeline)
		=> $"Mount: {FormatMountStatus(mounted, mountPipeline, unmountPipeline)}";

	public static string FormatNavLine(bool pathRunning, int waypoints, bool pathfindInProgress)
		=> $"Nav: {FormatNavProgress(pathRunning, waypoints, pathfindInProgress)}";

	public static string FormatBossModAvailable(bool available, string? providerName)
	{
		if (!available)
			return "BossMod: missing";
		if (!string.IsNullOrWhiteSpace(providerName))
			return $"BossMod: available ({providerName.Trim()})";
		return "BossMod: available";
	}

	public static string FormatBossModAi(bool active)
		=> active ? "BossMod AI: active" : "BossMod AI: idle";

	public static string FormatFakeHuntLine(bool active, bool fakeARankSet, string? summary)
	{
		if (!active)
			return "FakeHunt: off";
		var a = fakeARankSet ? "A-rank set" : "no A-rank";
		if (!string.IsNullOrWhiteSpace(summary))
			return $"FakeHunt: {summary.Trim()} ({a})";
		return $"FakeHunt: active ({a})";
	}

	/// <summary>Invoke <paramref name="get"/>; any throw → default idle snapshot.</summary>
	public static StatusSnapshot SafeCapture(Func<StatusSnapshot> get)
	{
		try
		{
			return get();
		}
		catch
		{
			return default;
		}
	}
}
