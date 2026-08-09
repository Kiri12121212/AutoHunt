#nullable enable

namespace HuntTrainAuto.Tests.Combat;

public sealed class RsrEnableDecisionTests
{
	[Fact]
	public void Decide_none_while_idle_and_not_started()
		=> Assert.Equal(RsrEnableKind.None, RsrEnableDecision.Decide(false, false));

	[Fact]
	public void Decide_none_while_in_combat_after_successful_start()
		=> Assert.Equal(RsrEnableKind.None, RsrEnableDecision.Decide(true, true));

	[Fact]
	public void Decide_start_auto_on_combat_phase_enter()
		=> Assert.Equal(RsrEnableKind.StartAuto, RsrEnableDecision.Decide(true, false));

	[Fact]
	public void Decide_stop_when_leaving_combat_after_start()
		=> Assert.Equal(RsrEnableKind.Stop, RsrEnableDecision.Decide(false, true));

	[Fact]
	public void Decide_does_not_start_without_combat_phase()
	{
		// Flag arrival / Following alone — not InCombatPhase, never started.
		Assert.Equal(RsrEnableKind.None, RsrEnableDecision.Decide(inCombatPhase: false, rotationAutoStarted: false));
	}

	[Fact]
	public void Describe_formats_action()
		=> Assert.Equal("action=StartAuto", RsrEnableDecision.Describe(RsrEnableKind.StartAuto));

	[Fact]
	public void Rising_edge_then_hold_only_starts_once_after_success()
	{
		Assert.Equal(RsrEnableKind.StartAuto, RsrEnableDecision.Decide(true, false));
		var started = RsrEnableDecision.NextRotationAutoStarted(RsrEnableKind.StartAuto, ipcSucceeded: true, false);
		Assert.True(started);
		Assert.Equal(RsrEnableKind.None, RsrEnableDecision.Decide(true, started));
	}

	[Fact]
	public void Failed_start_retries_while_still_in_combat()
	{
		Assert.Equal(RsrEnableKind.StartAuto, RsrEnableDecision.Decide(true, false));
		var started = RsrEnableDecision.NextRotationAutoStarted(RsrEnableKind.StartAuto, ipcSucceeded: false, false);
		Assert.False(started);
		Assert.Equal(RsrEnableKind.StartAuto, RsrEnableDecision.Decide(true, started));
		Assert.Equal(RsrEnableKind.StartAuto, RsrEnableDecision.Decide(true, false));
	}

	[Fact]
	public void Falling_edge_then_idle_only_stops_once_after_success()
	{
		Assert.Equal(RsrEnableKind.Stop, RsrEnableDecision.Decide(false, true));
		var started = RsrEnableDecision.NextRotationAutoStarted(RsrEnableKind.Stop, ipcSucceeded: true, true);
		Assert.False(started);
		Assert.Equal(RsrEnableKind.None, RsrEnableDecision.Decide(false, started));
	}

	[Fact]
	public void Failed_stop_retries_while_latch_held()
	{
		Assert.Equal(RsrEnableKind.Stop, RsrEnableDecision.Decide(false, true));
		var started = RsrEnableDecision.NextRotationAutoStarted(RsrEnableKind.Stop, ipcSucceeded: false, true);
		Assert.True(started);
		Assert.Equal(RsrEnableKind.Stop, RsrEnableDecision.Decide(false, started));
	}

	[Fact]
	public void Reenter_combat_starts_again()
	{
		Assert.Equal(RsrEnableKind.StartAuto, RsrEnableDecision.Decide(true, false));
		Assert.Equal(RsrEnableKind.Stop, RsrEnableDecision.Decide(false, true));
		Assert.Equal(RsrEnableKind.StartAuto, RsrEnableDecision.Decide(true, false));
	}

	[Fact]
	public void NextRotationAutoStarted_ignores_none_kind()
	{
		Assert.True(RsrEnableDecision.NextRotationAutoStarted(RsrEnableKind.None, ipcSucceeded: true, true));
		Assert.False(RsrEnableDecision.NextRotationAutoStarted(RsrEnableKind.None, ipcSucceeded: true, false));
	}

	[Fact]
	public void NextRotationAutoStarted_failed_ipc_keeps_latch()
	{
		Assert.False(RsrEnableDecision.NextRotationAutoStarted(RsrEnableKind.StartAuto, false, false));
		Assert.True(RsrEnableDecision.NextRotationAutoStarted(RsrEnableKind.Stop, false, true));
	}
}
