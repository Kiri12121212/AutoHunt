#nullable enable

namespace HuntTrainAuto.Tests.Combat;

public sealed class BossModEnableDecisionTests
{
	[Fact]
	public void Decide_none_while_idle_and_not_started()
		=> Assert.Equal(BossModEnableKind.None, BossModEnableDecision.Decide(false, false));

	[Fact]
	public void Decide_none_while_in_combat_after_successful_start()
		=> Assert.Equal(BossModEnableKind.None, BossModEnableDecision.Decide(true, true));

	[Fact]
	public void Decide_start_ai_on_combat_phase_enter()
		=> Assert.Equal(BossModEnableKind.StartAi, BossModEnableDecision.Decide(true, false));

	[Fact]
	public void Decide_stop_when_leaving_combat_after_start()
		=> Assert.Equal(BossModEnableKind.Stop, BossModEnableDecision.Decide(false, true));

	[Fact]
	public void Rising_edge_then_hold_only_starts_once_after_success()
	{
		Assert.Equal(BossModEnableKind.StartAi, BossModEnableDecision.Decide(true, false));
		var started = BossModEnableDecision.NextAiStarted(BossModEnableKind.StartAi, true, false);
		Assert.True(started);
		Assert.Equal(BossModEnableKind.None, BossModEnableDecision.Decide(true, started));
	}

	[Fact]
	public void Failed_start_retries_while_still_in_combat()
	{
		var started = BossModEnableDecision.NextAiStarted(BossModEnableKind.StartAi, false, false);
		Assert.False(started);
		Assert.Equal(BossModEnableKind.StartAi, BossModEnableDecision.Decide(true, started));
	}

	[Fact]
	public void Failed_stop_retries_while_latch_held()
	{
		var started = BossModEnableDecision.NextAiStarted(BossModEnableKind.Stop, false, true);
		Assert.True(started);
		Assert.Equal(BossModEnableKind.Stop, BossModEnableDecision.Decide(false, started));
	}

	[Fact]
	public void DecideClear_stops_when_latch_held()
	{
		Assert.Equal(BossModEnableKind.Stop, BossModEnableDecision.DecideClear(true));
		Assert.Equal(BossModEnableKind.None, BossModEnableDecision.DecideClear(false));
	}

	[Fact]
	public void NextAiStarted_ignores_none_kind()
	{
		Assert.True(BossModEnableDecision.NextAiStarted(BossModEnableKind.None, true, true));
		Assert.False(BossModEnableDecision.NextAiStarted(BossModEnableKind.None, true, false));
	}
}
