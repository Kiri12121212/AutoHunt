#nullable enable
using System;

namespace HuntTrainAuto.Tests.Windows;

public sealed class StatusDisplayTests
{
	[Theory]
	[InlineData(HuntTrainPhase.Idle, "Idle")]
	[InlineData(HuntTrainPhase.Teleport, "Teleport")]
	[InlineData(HuntTrainPhase.Mount, "Mount")]
	[InlineData(HuntTrainPhase.Navigate, "Navigate")]
	[InlineData(HuntTrainPhase.Unmount, "Unmount")]
	[InlineData(HuntTrainPhase.Combat, "Combat")]
	public void FormatPhase_labels_pipeline(HuntTrainPhase phase, string expected)
		=> Assert.Equal(expected, StatusDisplay.FormatPhase(phase));

	[Fact]
	public void FormatPhaseLine_prefixes_state()
		=> Assert.Equal("State: Navigate", StatusDisplay.FormatPhaseLine(HuntTrainPhase.Navigate));

	[Theory]
	[InlineData(false, MountPhase.Idle, UnmountPhase.Idle, "Not mounted")]
	[InlineData(true, MountPhase.Idle, UnmountPhase.Idle, "Mounted")]
	[InlineData(false, MountPhase.WaitReady, UnmountPhase.Idle, "Waiting to mount")]
	[InlineData(false, MountPhase.Mounting, UnmountPhase.Idle, "Mounting")]
	[InlineData(true, MountPhase.Idle, UnmountPhase.WaitReady, "Waiting to unmount")]
	[InlineData(true, MountPhase.Idle, UnmountPhase.Unmounting, "Unmounting")]
	[InlineData(true, MountPhase.Mounting, UnmountPhase.Unmounting, "Unmounting")]
	public void FormatMountStatus_prefers_pipeline(
		bool mounted,
		MountPhase mountPipeline,
		UnmountPhase unmountPipeline,
		string expected)
		=> Assert.Equal(
			expected,
			StatusDisplay.FormatMountStatus(mounted, mountPipeline, unmountPipeline));

	[Fact]
	public void FormatMountLine_prefixes_mount()
		=> Assert.Equal(
			"Mount: Mounted",
			StatusDisplay.FormatMountLine(true, MountPhase.Idle, UnmountPhase.Idle));

	[Theory]
	[InlineData(false, 0, false, "Idle")]
	[InlineData(false, 0, true, "Pathfinding…")]
	[InlineData(true, 0, false, "Navigating")]
	[InlineData(true, 4, false, "Navigating (4 waypoints)")]
	[InlineData(true, 4, true, "Pathfinding…")]
	public void FormatNavProgress_path_states(
		bool running,
		int waypoints,
		bool pathfind,
		string expected)
		=> Assert.Equal(expected, StatusDisplay.FormatNavProgress(running, waypoints, pathfind));

	[Fact]
	public void FormatNavLine_prefixes_nav()
		=> Assert.Equal(
			"Nav: Idle",
			StatusDisplay.FormatNavLine(false, 0, false));

	[Theory]
	[InlineData(false, null, "BossMod: missing")]
	[InlineData(true, null, "BossMod: available")]
	[InlineData(true, "BossMod Reborn", "BossMod: available (BossMod Reborn)")]
	public void FormatBossModAvailable_labels(bool available, string? name, string expected)
		=> Assert.Equal(expected, StatusDisplay.FormatBossModAvailable(available, name));

	[Theory]
	[InlineData(false, "BossMod AI: idle")]
	[InlineData(true, "BossMod AI: active")]
	public void FormatBossModAi_labels(bool active, string expected)
		=> Assert.Equal(expected, StatusDisplay.FormatBossModAi(active));

	[Fact]
	public void SafeCapture_returns_probe_result()
	{
		var snap = new StatusSnapshot
		{
			Phase = HuntTrainPhase.Combat,
			Mounted = true,
		};
		Assert.Equal(HuntTrainPhase.Combat, StatusDisplay.SafeCapture(() => snap).Phase);
	}

	[Fact]
	public void SafeCapture_swallows_exceptions()
	{
		var snap = StatusDisplay.SafeCapture(
			() => throw new InvalidOperationException("ui probe"));
		Assert.Equal(HuntTrainPhase.Idle, snap.Phase);
		Assert.False(snap.Mounted);
		Assert.False(snap.NavPathRunning);
	}
}
