#nullable enable

namespace HuntTrainAuto.Tests.State;

public sealed class HuntTrainObserveTests
{
	[Fact]
	public void DecideFlagStart_master_off_is_None()
	{
		Assert.Equal(
			HuntTrainEvent.None,
			HuntTrainObserve.DecideFlagStart(
				pluginEnabled: false,
				teleportPlanActive: true,
				alreadyCloseSkip: true,
				useMount: true));
	}

	[Fact]
	public void DecideFlagStart_teleport_plan_starts_Teleport()
	{
		Assert.Equal(
			HuntTrainEvent.StartTeleport,
			HuntTrainObserve.DecideFlagStart(
				pluginEnabled: true,
				teleportPlanActive: true,
				alreadyCloseSkip: false,
				useMount: true));
	}

	[Fact]
	public void DecideFlagStart_teleport_beats_AlreadyClose()
	{
		Assert.Equal(
			HuntTrainEvent.StartTeleport,
			HuntTrainObserve.DecideFlagStart(
				pluginEnabled: true,
				teleportPlanActive: true,
				alreadyCloseSkip: true,
				useMount: true));
	}

	[Fact]
	public void DecideFlagStart_AlreadyClose_with_mount_starts_Mount()
	{
		Assert.Equal(
			HuntTrainEvent.StartMount,
			HuntTrainObserve.DecideFlagStart(
				pluginEnabled: true,
				teleportPlanActive: false,
				alreadyCloseSkip: true,
				useMount: true));
	}

	[Fact]
	public void DecideFlagStart_AlreadyClose_without_mount_starts_Navigate()
	{
		Assert.Equal(
			HuntTrainEvent.StartNavigate,
			HuntTrainObserve.DecideFlagStart(
				pluginEnabled: true,
				teleportPlanActive: false,
				alreadyCloseSkip: true,
				useMount: false));
	}

	[Fact]
	public void DecideFlagStart_no_plan_no_close_is_None()
	{
		Assert.Equal(
			HuntTrainEvent.None,
			HuntTrainObserve.DecideFlagStart(
				pluginEnabled: true,
				teleportPlanActive: false,
				alreadyCloseSkip: false,
				useMount: true));
	}

	[Fact]
	public void BuildProgressSnapshot_maps_runner_signals()
	{
		var snap = HuntTrainObserve.BuildProgressSnapshot(
			pluginEnabled: true,
			abort: false,
			teleportPlanActive: false,
			mountJobActive: false,
			withinFlagArrival: true,
			readyForGroundFollow: true,
			inCombatPhase: false);

		Assert.True(snap.PluginEnabled);
		Assert.False(snap.Abort);
		Assert.True(snap.TeleportComplete);
		Assert.True(snap.MountComplete);
		Assert.True(snap.WithinFlagArrival);
		Assert.True(snap.ReadyForGroundFollow);
		Assert.False(snap.PartyEngaged);
		Assert.True(snap.CombatEnded);
		Assert.False(snap.NeedsTeleport);
		Assert.False(snap.SameZoneReady);
		Assert.False(snap.AlreadyMountedOrSkipMount);
	}

	[Fact]
	public void BuildProgressSnapshot_active_plan_and_mount_block_complete()
	{
		var snap = HuntTrainObserve.BuildProgressSnapshot(
			pluginEnabled: true,
			teleportPlanActive: true,
			mountJobActive: true,
			inCombatPhase: true);

		Assert.False(snap.TeleportComplete);
		Assert.False(snap.MountComplete);
		Assert.True(snap.PartyEngaged);
		Assert.False(snap.CombatEnded);
	}

	[Fact]
	public void BuildProgressSnapshot_mounted_completes_mount_even_when_job_active()
	{
		var snap = HuntTrainObserve.BuildProgressSnapshot(
			pluginEnabled: true,
			mountJobActive: true,
			mounted: true);

		Assert.True(snap.MountComplete);
	}

	[Fact]
	public void BuildProgressSnapshot_master_off_aborts_active_via_Tick()
	{
		var snap = HuntTrainObserve.BuildProgressSnapshot(pluginEnabled: false);
		Assert.Equal(HuntTrainPhase.Idle, HuntTrainTransition.Tick(HuntTrainPhase.Navigate, snap));
	}

	[Fact]
	public void BuildProgressSnapshot_drives_full_progress_pipeline()
	{
		var phase = HuntTrainPhase.Teleport;
		phase = HuntTrainTransition.Tick(
			phase,
			HuntTrainObserve.BuildProgressSnapshot(pluginEnabled: true, teleportPlanActive: false));
		Assert.Equal(HuntTrainPhase.Mount, phase);

		phase = HuntTrainTransition.Tick(
			phase,
			HuntTrainObserve.BuildProgressSnapshot(pluginEnabled: true, mountJobActive: false));
		Assert.Equal(HuntTrainPhase.Navigate, phase);

		phase = HuntTrainTransition.Tick(
			phase,
			HuntTrainObserve.BuildProgressSnapshot(pluginEnabled: true, withinFlagArrival: true));
		Assert.Equal(HuntTrainPhase.Unmount, phase);

		phase = HuntTrainTransition.Tick(
			phase,
			HuntTrainObserve.BuildProgressSnapshot(pluginEnabled: true, readyForGroundFollow: true));
		Assert.Equal(HuntTrainPhase.FollowParty, phase);

		phase = HuntTrainTransition.Tick(
			phase,
			HuntTrainObserve.BuildProgressSnapshot(pluginEnabled: true, inCombatPhase: true));
		Assert.Equal(HuntTrainPhase.Combat, phase);

		phase = HuntTrainTransition.Tick(
			phase,
			HuntTrainObserve.BuildProgressSnapshot(pluginEnabled: true, inCombatPhase: false));
		Assert.Equal(HuntTrainPhase.Idle, phase);
	}

	[Fact]
	public void Default_BuildProgressSnapshot_is_soft_noop_when_Idle()
	{
		var snap = HuntTrainObserve.BuildProgressSnapshot(pluginEnabled: true);
		Assert.Equal(HuntTrainEvent.None, HuntTrainTransition.Decide(HuntTrainPhase.Idle, snap));
	}
}
