#nullable enable

namespace HuntTrainAuto.Tests.Combat;

public sealed class RsrStopDecisionTests
{
	[Theory]
	[InlineData(RsrStopTrigger.FlagChange, RsrStopPath.ImmediateClear)]
	[InlineData(RsrStopTrigger.TerritoryLeave, RsrStopPath.ImmediateClear)]
	[InlineData(RsrStopTrigger.MasterOff, RsrStopPath.ImmediateClear)]
	[InlineData(RsrStopTrigger.Dispose, RsrStopPath.ImmediateClear)]
	[InlineData(RsrStopTrigger.Death, RsrStopPath.CombatPhaseTick)]
	[InlineData(RsrStopTrigger.CombatPhaseExit, RsrStopPath.CombatPhaseTick)]
	[InlineData(RsrStopTrigger.None, RsrStopPath.None)]
	public void PathFor_maps_tasks_6_5_triggers(RsrStopTrigger trigger, RsrStopPath expected)
		=> Assert.Equal(expected, RsrStopDecision.PathFor(trigger));

	[Fact]
	public void ShouldAttemptStop_only_when_latch_held()
	{
		Assert.False(RsrStopDecision.ShouldAttemptStop(false));
		Assert.True(RsrStopDecision.ShouldAttemptStop(true));
	}

	[Fact]
	public void DecideClear_stop_only_when_started()
	{
		Assert.Equal(RsrEnableKind.None, RsrStopDecision.DecideClear(false));
		Assert.Equal(RsrEnableKind.Stop, RsrStopDecision.DecideClear(true));
	}

	[Fact]
	public void DecideClear_soft_fail_keeps_latch_for_retry()
	{
		var kind = RsrStopDecision.DecideClear(true);
		Assert.Equal(RsrEnableKind.Stop, kind);
		var latch = RsrEnableDecision.NextRotationAutoStarted(kind, ipcSucceeded: false, true);
		Assert.True(latch);
		Assert.Equal(RsrEnableKind.Stop, RsrStopDecision.DecideClear(latch));
	}

	[Fact]
	public void DecideClear_success_clears_latch()
	{
		var kind = RsrStopDecision.DecideClear(true);
		var latch = RsrEnableDecision.NextRotationAutoStarted(kind, ipcSucceeded: true, true);
		Assert.False(latch);
		Assert.Equal(RsrEnableKind.None, RsrStopDecision.DecideClear(latch));
	}

	[Fact]
	public void DecideTick_matches_enable_decision_for_phase_exit()
	{
		Assert.Equal(
			RsrEnableDecision.Decide(false, true),
			RsrStopDecision.DecideTick(false, true));
		Assert.Equal(RsrEnableKind.Stop, RsrStopDecision.DecideTick(false, true));
		Assert.Equal(RsrEnableKind.None, RsrStopDecision.DecideTick(false, false));
		Assert.Equal(RsrEnableKind.StartAuto, RsrStopDecision.DecideTick(true, false));
		Assert.Equal(RsrEnableKind.None, RsrStopDecision.DecideTick(true, true));
	}

	[Fact]
	public void Death_path_stops_via_combat_phase_exit_tick()
	{
		Assert.Equal(RsrStopPath.CombatPhaseTick, RsrStopDecision.PathFor(RsrStopTrigger.Death));

		// CombatDecision → Idle (player dead); RSR Tick sees !InCombatPhase + latch.
		Assert.Equal(
			CombatTransitionKind.StopFollow,
			CombatDecision.Decide(CombatPhase.Combat, DeadSnap()));
		Assert.Equal(
			CombatPhase.Idle,
			CombatDecision.NextPhase(CombatPhase.Combat, CombatTransitionKind.StopFollow));
		Assert.Equal(RsrEnableKind.Stop, RsrStopDecision.DecideTick(inCombatPhase: false, rotationAutoStarted: true));
	}

	[Fact]
	public void Mob_dead_combat_end_stops_via_combat_phase_exit_tick()
	{
		Assert.Equal(RsrStopPath.CombatPhaseTick, RsrStopDecision.PathFor(RsrStopTrigger.CombatPhaseExit));

		Assert.True(CombatDecision.IsCombatEnded(EndedSnap()));
		Assert.Equal(
			CombatTransitionKind.StopFollow,
			CombatDecision.Decide(CombatPhase.Combat, EndedSnap()));
		Assert.Equal(RsrEnableKind.Stop, RsrStopDecision.DecideTick(inCombatPhase: false, rotationAutoStarted: true));
	}

	[Fact]
	public void Abort_triggers_use_ImmediateClear_not_tick_only()
	{
		Assert.Equal(RsrStopPath.ImmediateClear, RsrStopDecision.PathFor(RsrStopTrigger.FlagChange));
		Assert.Equal(RsrStopPath.ImmediateClear, RsrStopDecision.PathFor(RsrStopTrigger.TerritoryLeave));
		Assert.Equal(RsrStopPath.ImmediateClear, RsrStopDecision.PathFor(RsrStopTrigger.MasterOff));
		Assert.Equal(RsrStopPath.ImmediateClear, RsrStopDecision.PathFor(RsrStopTrigger.Dispose));

		// ImmediateClear still goes through DecideClear → shared soft-fail latch.
		Assert.Equal(RsrEnableKind.Stop, RsrStopDecision.DecideClear(true));
	}

	[Fact]
	public void Failed_clear_then_tick_retries_stop_without_restart_while_out_of_combat()
	{
		var latch = true;
		var clearKind = RsrStopDecision.DecideClear(latch);
		latch = RsrEnableDecision.NextRotationAutoStarted(clearKind, ipcSucceeded: false, latch);
		Assert.True(latch);

		// Still out of combat (new flag abort): Tick retries Stop, does not StartAuto.
		Assert.Equal(RsrEnableKind.Stop, RsrStopDecision.DecideTick(false, latch));
	}

	private static CombatEngageSnapshot DeadSnap()
		=> new()
		{
			PluginEnabled = true,
			PlayerDead = true,
			EngageRange = CombatDecision.DefaultEngageRange,
		};

	private static CombatEngageSnapshot EndedSnap()
		=> new()
		{
			PluginEnabled = true,
			PlayerDead = false,
			PlayerInCombat = false,
			AnyPartyAllyInCombat = false,
			LatchedEngageTargetInCombat = false,
			PartyTargetsHuntMob = false,
			EngageRange = CombatDecision.DefaultEngageRange,
		};
}
