#nullable enable

namespace HuntTrainAuto.Tests.State;

public sealed class FlagRestartDecisionTests
{
	[Fact]
	public void IsPipelineActive_Idle_without_inflight_is_false()
	{
		Assert.False(FlagRestartDecision.IsPipelineActive(HuntTrainPhase.Idle, hasInFlightWork: false));
	}

	[Fact]
	public void IsPipelineActive_non_Idle_is_true()
	{
		Assert.True(FlagRestartDecision.IsPipelineActive(HuntTrainPhase.Navigate, hasInFlightWork: false));
		Assert.True(FlagRestartDecision.IsPipelineActive(HuntTrainPhase.Combat, hasInFlightWork: false));
	}

	[Fact]
	public void IsPipelineActive_Idle_with_inflight_is_true()
	{
		Assert.True(FlagRestartDecision.IsPipelineActive(HuntTrainPhase.Idle, hasInFlightWork: true));
	}

	[Fact]
	public void Decide_Idle_start_from_idle_clears_jobs_without_abort()
	{
		var plan = FlagRestartDecision.Decide(
			pluginEnabled: true,
			phase: HuntTrainPhase.Idle,
			hasInFlightWork: false,
			teleportPlanActive: true,
			alreadyCloseSkip: false,
			useMount: true);

		Assert.Equal(FlagRestartKind.StartFromIdle, plan.Kind);
		Assert.Equal(HuntTrainEvent.StartMount, plan.StartEvent);
		Assert.False(plan.StopNavPath);
		Assert.False(plan.ResetTrainController);
		AssertJobClears(plan);
	}

	[Fact]
	public void Decide_Idle_teleport_already_mounted_starts_Teleport()
	{
		var plan = FlagRestartDecision.Decide(
			pluginEnabled: true,
			phase: HuntTrainPhase.Idle,
			hasInFlightWork: false,
			teleportPlanActive: true,
			alreadyCloseSkip: false,
			useMount: true,
			alreadyMountedOrSkipMount: true);

		Assert.Equal(FlagRestartKind.StartFromIdle, plan.Kind);
		Assert.Equal(HuntTrainEvent.StartTeleport, plan.StartEvent);
	}

	[Fact]
	public void Decide_AlreadyClose_Idle_starts_Mount()
	{
		var plan = FlagRestartDecision.Decide(
			pluginEnabled: true,
			phase: HuntTrainPhase.Idle,
			hasInFlightWork: false,
			teleportPlanActive: false,
			alreadyCloseSkip: true,
			useMount: true);

		Assert.Equal(FlagRestartKind.StartFromIdle, plan.Kind);
		Assert.Equal(HuntTrainEvent.StartMount, plan.StartEvent);
	}

	[Fact]
	public void Decide_mid_pipeline_aborts_then_restarts()
	{
		var plan = FlagRestartDecision.Decide(
			pluginEnabled: true,
			phase: HuntTrainPhase.Unmount,
			hasInFlightWork: false,
			teleportPlanActive: false,
			alreadyCloseSkip: true,
			useMount: false);

		Assert.Equal(FlagRestartKind.AbortThenRestart, plan.Kind);
		Assert.Equal(HuntTrainEvent.StartNavigate, plan.StartEvent);
		Assert.True(plan.StopNavPath);
		Assert.True(plan.ResetTrainController);
		AssertJobClears(plan);
	}

	[Fact]
	public void Decide_Idle_with_inflight_still_aborts()
	{
		var plan = FlagRestartDecision.Decide(
			pluginEnabled: true,
			phase: HuntTrainPhase.Idle,
			hasInFlightWork: true,
			teleportPlanActive: true,
			alreadyCloseSkip: false,
			useMount: true);

		Assert.Equal(FlagRestartKind.AbortThenRestart, plan.Kind);
		Assert.Equal(HuntTrainEvent.StartMount, plan.StartEvent);
		Assert.True(plan.StopNavPath);
		Assert.True(plan.ResetTrainController);
	}

	[Fact]
	public void Decide_master_off_StartEvent_None_still_aborts_active()
	{
		var plan = FlagRestartDecision.Decide(
			pluginEnabled: false,
			phase: HuntTrainPhase.Combat,
			hasInFlightWork: false,
			teleportPlanActive: true,
			alreadyCloseSkip: true,
			useMount: true);

		Assert.Equal(FlagRestartKind.AbortThenRestart, plan.Kind);
		Assert.Equal(HuntTrainEvent.None, plan.StartEvent);
		Assert.True(plan.ResetTrainController);
	}

	[Fact]
	public void Abort_then_Apply_Start_advances_from_Idle()
	{
		var c = new HuntTrainController();
		c.Apply(HuntTrainEvent.StartNavigate);
		Assert.Equal(HuntTrainPhase.Navigate, c.Phase);

		var plan = FlagRestartDecision.Decide(
			pluginEnabled: true,
			phase: c.Phase,
			hasInFlightWork: false,
			teleportPlanActive: true,
			alreadyCloseSkip: false,
			useMount: true);

		Assert.Equal(FlagRestartKind.AbortThenRestart, plan.Kind);
		Assert.Equal(HuntTrainEvent.StartMount, plan.StartEvent);
		if (plan.ResetTrainController)
			c.Reset();
		Assert.Equal(HuntTrainPhase.Idle, c.Phase);
		Assert.Equal(HuntTrainPhase.Mount, c.Apply(plan.StartEvent));
	}

	[Fact]
	public void Without_abort_StartTeleport_from_Mount_advances_mount_before_tp()
	{
		// Mount→Teleport is legal (mount-before-TP). Other Start* from wrong phases stay put.
		var c = new HuntTrainController();
		c.Apply(HuntTrainEvent.StartMount);
		Assert.Equal(HuntTrainPhase.Mount, c.Phase);
		Assert.Equal(HuntTrainPhase.Teleport, c.Apply(HuntTrainEvent.StartTeleport));
	}

	[Fact]
	public void Factory_helpers_match_Decide()
	{
		Assert.Equal(
			FlagRestartKind.StartFromIdle,
			FlagRestartDecision.StartFromIdle(HuntTrainEvent.StartMount).Kind);
		Assert.Equal(
			FlagRestartKind.AbortThenRestart,
			FlagRestartDecision.AbortThenRestart(HuntTrainEvent.StartTeleport).Kind);
	}

	private static void AssertJobClears(FlagRestartPlan plan)
	{
		Assert.True(plan.ClearInstanceChange);
		Assert.True(plan.ClearMount);
		Assert.True(plan.ClearFlagArrival);
		Assert.True(plan.ClearUnmount);
		Assert.True(plan.ClearEngage);
		Assert.True(plan.ClearCombat);
		Assert.True(plan.ClearRsr);
	}
}
