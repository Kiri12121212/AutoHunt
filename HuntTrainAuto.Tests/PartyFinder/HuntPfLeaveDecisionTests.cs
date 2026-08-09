#nullable enable
using HuntTrainAuto.PartyFinder;

namespace HuntTrainAuto.Tests.PartyFinder;

public sealed class HuntPfLeaveDecisionTests
{
	[Fact]
	public void Decide_none_when_disabled()
		=> Assert.Equal(
			HuntPfLeaveKind.None,
			HuntPfLeaveDecision.Decide(
				enabled: false,
				inParty: true,
				sessionActive: true,
				armedLastStop: true,
				wasInCombat: true,
				inCombat: false,
				nowMs: 10_000,
				lastCombatEndMs: 9_000,
				lastFlagMs: 0,
				idleTimeoutMs: HuntPfLeaveDecision.DefaultIdleLeaveMs,
				actionReady: true));

	[Fact]
	public void Decide_none_on_bare_combat_end_without_arm()
		=> Assert.Equal(
			HuntPfLeaveKind.None,
			HuntPfLeaveDecision.Decide(
				enabled: true,
				inParty: true,
				sessionActive: true,
				armedLastStop: false,
				wasInCombat: true,
				inCombat: false,
				nowMs: 10_000,
				lastCombatEndMs: 10_000,
				lastFlagMs: 0,
				idleTimeoutMs: HuntPfLeaveDecision.DefaultIdleLeaveMs,
				actionReady: true));

	[Fact]
	public void Decide_leave_after_armed_combat_end()
		=> Assert.Equal(
			HuntPfLeaveKind.LeaveAfterArmedCombatEnd,
			HuntPfLeaveDecision.Decide(
				enabled: true,
				inParty: true,
				sessionActive: true,
				armedLastStop: true,
				wasInCombat: true,
				inCombat: false,
				nowMs: 10_000,
				lastCombatEndMs: 10_000,
				lastFlagMs: 0,
				idleTimeoutMs: HuntPfLeaveDecision.DefaultIdleLeaveMs,
				actionReady: true));

	[Fact]
	public void Decide_none_while_still_in_combat_even_when_armed()
		=> Assert.Equal(
			HuntPfLeaveKind.None,
			HuntPfLeaveDecision.Decide(
				enabled: true,
				inParty: true,
				sessionActive: true,
				armedLastStop: true,
				wasInCombat: true,
				inCombat: true,
				nowMs: 10_000,
				lastCombatEndMs: 0,
				lastFlagMs: 0,
				idleTimeoutMs: HuntPfLeaveDecision.DefaultIdleLeaveMs,
				actionReady: true));

	[Fact]
	public void Decide_idle_timeout_after_combat_with_no_newer_flag()
		=> Assert.Equal(
			HuntPfLeaveKind.LeaveIdleTimeout,
			HuntPfLeaveDecision.Decide(
				enabled: true,
				inParty: true,
				sessionActive: true,
				armedLastStop: false,
				wasInCombat: false,
				inCombat: false,
				nowMs: 700_000,
				lastCombatEndMs: 100_000,
				lastFlagMs: 50_000,
				idleTimeoutMs: HuntPfLeaveDecision.DefaultIdleLeaveMs,
				actionReady: true));

	[Fact]
	public void Decide_idle_reset_by_newer_flag()
		=> Assert.Equal(
			HuntPfLeaveKind.None,
			HuntPfLeaveDecision.Decide(
				enabled: true,
				inParty: true,
				sessionActive: true,
				armedLastStop: false,
				wasInCombat: false,
				inCombat: false,
				nowMs: 700_000,
				lastCombatEndMs: 100_000,
				lastFlagMs: 650_000,
				idleTimeoutMs: HuntPfLeaveDecision.DefaultIdleLeaveMs,
				actionReady: true));

	[Fact]
	public void Decide_clear_latch_when_party_gone()
		=> Assert.Equal(
			HuntPfLeaveKind.ClearLatchOnly,
			HuntPfLeaveDecision.Decide(
				enabled: true,
				inParty: false,
				sessionActive: true,
				armedLastStop: true,
				wasInCombat: false,
				inCombat: false,
				nowMs: 10_000,
				lastCombatEndMs: 0,
				lastFlagMs: 0,
				idleTimeoutMs: HuntPfLeaveDecision.DefaultIdleLeaveMs,
				actionReady: true));

	[Theory]
	[InlineData(true, false, true)]
	[InlineData(true, true, false)]
	[InlineData(false, false, false)]
	public void ShouldNoteCombatEnd(bool wasInCombat, bool inCombat, bool expected)
		=> Assert.Equal(
			expected,
			HuntPfLeaveDecision.ShouldNoteCombatEnd(wasInCombat, inCombat));

	[Theory]
	[InlineData(0, HuntPfLeaveDecision.MinIdleLeaveMs)]
	[InlineData(HuntPfLeaveDecision.DefaultIdleLeaveMs, HuntPfLeaveDecision.DefaultIdleLeaveMs)]
	[InlineData(99_999_999, HuntPfLeaveDecision.MaxIdleLeaveMs)]
	public void ClampIdleLeaveMs(int input, int expected)
		=> Assert.Equal(expected, HuntPfLeaveDecision.ClampIdleLeaveMs(input));

	[Fact]
	public void Describe_reports_action()
		=> Assert.Equal(
			"action=LeaveIdleTimeout",
			HuntPfLeaveDecision.Describe(HuntPfLeaveKind.LeaveIdleTimeout));
}
