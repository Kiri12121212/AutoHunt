#nullable enable

namespace HuntTrainAuto.Tests.State;

public sealed class TerritoryCleanupDecisionTests
{
	[Fact]
	public void Decide_stay_hunting_noop_when_no_plan_and_no_flag()
	{
		var plan = TerritoryCleanupDecision.Decide(
			teleportPlanActive: false,
			isHuntingTerritory: true,
			hasActiveHuntFlag: false);

		Assert.Equal(TerritoryCleanupKind.StayHuntingNoop, plan.Kind);
		Assert.False(plan.ClearTeleportPlan);
		Assert.False(plan.EnqueueMount);
		Assert.False(plan.ResetTrainController);
		Assert.False(plan.ClearConductors);
		Assert.False(plan.StopNavPath);
		Assert.False(plan.InvalidateFlagWorldPos);
		Assert.False(plan.ClearEngage);
		Assert.False(plan.ClearCombat);
		Assert.False(plan.SaveConfig);
	}

	[Fact]
	public void Decide_hunting_mesh_reload_when_flag_without_plan()
	{
		// BetweenAreas cleared TeleportPlan before OnTerritoryChanged.
		var plan = TerritoryCleanupDecision.Decide(
			teleportPlanActive: false,
			isHuntingTerritory: true,
			hasActiveHuntFlag: true);

		Assert.Equal(TerritoryCleanupKind.HuntingMeshReload, plan.Kind);
		Assert.True(plan.StopNavPath);
		Assert.True(plan.InvalidateFlagWorldPos);
		Assert.True(plan.ClearFlagArrival);
		Assert.False(plan.ClearTeleportPlan);
		Assert.False(plan.EnqueueMount);
		Assert.False(plan.ResetTrainController);
		Assert.False(plan.ClearActiveHuntFlag);
		Assert.False(plan.ClearConductors);
	}

	[Fact]
	public void Decide_tp_arrival_handoff_into_hunting()
	{
		var plan = TerritoryCleanupDecision.Decide(
			teleportPlanActive: true,
			isHuntingTerritory: true);

		Assert.Equal(TerritoryCleanupKind.TpArrivalHandoff, plan.Kind);
		Assert.True(plan.ClearTeleportPlan);
		Assert.True(plan.EnqueueInstanceChangeIfNeeded);
		Assert.True(plan.EnqueueMount);
		Assert.True(plan.StopNavPath);
		Assert.True(plan.InvalidateFlagWorldPos);
		Assert.True(plan.ClearEngage);
		Assert.True(plan.ClearCombat);
		Assert.True(plan.ClearRsr);
		Assert.True(plan.ClearFlagArrival);
		Assert.True(plan.ClearUnmount);
		// Handoff must not undo mount / instance / train progress.
		Assert.False(plan.ClearMount);
		Assert.False(plan.ClearInstanceChange);
		Assert.False(plan.ResetTrainController);
		Assert.False(plan.ClearConductors);
		Assert.False(plan.ClearActiveHuntFlag);
		Assert.False(plan.SaveConfig);
	}

	[Fact]
	public void Decide_leave_hunting_full_abort()
	{
		var plan = TerritoryCleanupDecision.Decide(
			teleportPlanActive: false,
			isHuntingTerritory: false);

		Assert.Equal(TerritoryCleanupKind.LeaveHuntingFull, plan.Kind);
		AssertLeaveFull(plan);
	}

	[Fact]
	public void Decide_non_hunting_with_active_plan_is_full_abort_not_handoff()
	{
		// Wrong destination / city: clear plan remnants; do not enqueue mount.
		var plan = TerritoryCleanupDecision.Decide(
			teleportPlanActive: true,
			isHuntingTerritory: false);

		Assert.Equal(TerritoryCleanupKind.LeaveHuntingFull, plan.Kind);
		Assert.True(plan.ClearTeleportPlan);
		Assert.False(plan.EnqueueMount);
		Assert.False(plan.EnqueueInstanceChangeIfNeeded);
		AssertLeaveFull(plan);
	}

	[Fact]
	public void Leave_prefers_stop_path_over_forced_dismount()
	{
		var plan = TerritoryCleanupDecision.LeaveHuntingFull();
		Assert.True(plan.StopNavPath);
		Assert.True(plan.ClearUnmount);
		Assert.True(plan.ClearMount);
		// ClearMount / ClearUnmount are job clears — Plugin must not force dismount mid-load.
	}

	[Fact]
	public void Factory_helpers_match_Decide()
	{
		Assert.Equal(
			TerritoryCleanupKind.StayHuntingNoop,
			TerritoryCleanupDecision.StayHuntingNoop().Kind);
		Assert.Equal(
			TerritoryCleanupKind.TpArrivalHandoff,
			TerritoryCleanupDecision.TpArrivalHandoff().Kind);
		Assert.Equal(
			TerritoryCleanupKind.HuntingMeshReload,
			TerritoryCleanupDecision.HuntingMeshReload().Kind);
		Assert.Equal(
			TerritoryCleanupKind.LeaveHuntingFull,
			TerritoryCleanupDecision.LeaveHuntingFull().Kind);
	}

	private static void AssertLeaveFull(TerritoryCleanupPlan plan)
	{
		Assert.True(plan.ClearTeleportPlan);
		Assert.True(plan.ClearInstanceChange);
		Assert.True(plan.ClearMount);
		Assert.True(plan.ClearActiveHuntFlag);
		Assert.True(plan.ClearFlagArrival);
		Assert.True(plan.ClearUnmount);
		Assert.True(plan.ClearEngage);
		Assert.True(plan.ClearCombat);
		Assert.True(plan.ClearRsr);
		Assert.True(plan.StopNavPath);
		Assert.True(plan.ClearConductors);
		Assert.True(plan.ResetTrainController);
		Assert.True(plan.SaveConfig);
		Assert.False(plan.EnqueueMount);
		Assert.False(plan.EnqueueInstanceChangeIfNeeded);
	}
}
