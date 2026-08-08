#nullable enable

namespace HuntTrainAuto.Tests.Teleport;

public sealed class InstanceChangeDecisionTests
{
	[Theory]
	[InlineData(0, false)]
	[InlineData(1, true)]
	[InlineData(3, true)]
	public void ShouldEnqueue(int instance, bool expected)
		=> Assert.Equal(expected, InstanceChangeDecision.ShouldEnqueue(instance));

	[Theory]
	[InlineData(2, 2, true)]
	[InlineData(2, 1, false)]
	[InlineData(0, 0, false)]
	[InlineData(0, 1, false)]
	public void IsAlreadyOnInstance(int requested, int current, bool expected)
		=> Assert.Equal(expected, InstanceChangeDecision.IsAlreadyOnInstance(requested, current));

	[Theory]
	[InlineData(2, 1, 3, true)]
	[InlineData(2, 2, 3, false)]
	[InlineData(0, 1, 3, false)]
	[InlineData(2, 1, 0, true)]
	[InlineData(3, 1, 2, false)]
	[InlineData(0, 0, 0, false)]
	public void NeedsInstanceChange(int requested, int current, int count, bool expected)
		=> Assert.Equal(expected, InstanceChangeDecision.NeedsInstanceChange(requested, current, count));

	[Theory]
	[InlineData(false, 10u, true)]
	[InlineData(true, 10u, false)]
	[InlineData(false, 0u, false)]
	[InlineData(true, 0u, false)]
	public void ShouldAetheryteTeleportForInstanceSwitch(bool canChange, uint aetheryteId, bool expected)
		=> Assert.Equal(
			expected,
			InstanceChangeDecision.ShouldAetheryteTeleportForInstanceSwitch(canChange, aetheryteId));

	[Theory]
	[InlineData(true, true, false, true)]
	[InlineData(false, true, false, false)]
	[InlineData(true, false, false, false)]
	[InlineData(true, true, true, false)]
	public void IsLanded(bool territory, bool ready, bool between, bool expected)
		=> Assert.Equal(expected, InstanceChangeDecision.IsLanded(territory, ready, between));

	[Fact]
	public void DecideApproach_ready_when_can_change()
	{
		Assert.Equal(
			InstanceChangeDecision.ApproachAction.ReadyToChange,
			InstanceChangeDecision.DecideApproach(true, false, false, true, true));
	}

	[Fact]
	public void DecideApproach_soft_abort_without_aetheryte()
	{
		Assert.Equal(
			InstanceChangeDecision.ApproachAction.SoftAbortNoAetheryte,
			InstanceChangeDecision.DecideApproach(false, false, false, true, true));
	}

	[Fact]
	public void DecideApproach_set_target_then_lockon()
	{
		Assert.Equal(
			InstanceChangeDecision.ApproachAction.SetTarget,
			InstanceChangeDecision.DecideApproach(false, true, false, true, true));
		Assert.Equal(
			InstanceChangeDecision.ApproachAction.Wait,
			InstanceChangeDecision.DecideApproach(false, true, false, false, true));
		Assert.Equal(
			InstanceChangeDecision.ApproachAction.LockonAndAutomove,
			InstanceChangeDecision.DecideApproach(false, true, true, true, true));
		Assert.Equal(
			InstanceChangeDecision.ApproachAction.Wait,
			InstanceChangeDecision.DecideApproach(false, true, true, true, false));
	}

	[Fact]
	public void DecideChangeTick_succeed_timeout_issue()
	{
		Assert.Equal(
			InstanceChangeDecision.ChangeTickResult.Succeeded,
			InstanceChangeDecision.DecideChangeTick(true, false, false, false));
		Assert.Equal(
			InstanceChangeDecision.ChangeTickResult.TimedOut,
			InstanceChangeDecision.DecideChangeTick(false, true, false, true));
		Assert.Equal(
			InstanceChangeDecision.ChangeTickResult.IssueChange,
			InstanceChangeDecision.DecideChangeTick(false, true, false, false));
		Assert.Equal(
			InstanceChangeDecision.ChangeTickResult.Continue,
			InstanceChangeDecision.DecideChangeTick(false, true, true, false));
		Assert.Equal(
			InstanceChangeDecision.ChangeTickResult.Continue,
			InstanceChangeDecision.DecideChangeTick(false, false, false, false));
	}
}

public sealed class InstanceChangeSessionTests
{
	[Fact]
	public void Enqueue_enters_WaitLanded_and_Clear_resets()
	{
		var session = new InstanceChangeSession();
		Assert.False(session.IsActive);

		session.Enqueue(2, 813);
		Assert.True(session.IsActive);
		Assert.Equal(InstanceChangePhase.WaitLanded, session.Phase);
		Assert.Equal(2, session.Instance);
		Assert.Equal(813u, session.Territory);

		session.EnterApproach();
		Assert.Equal(InstanceChangePhase.Approach, session.Phase);

		session.EnterChanging(nowMs: 1000);
		Assert.Equal(InstanceChangePhase.Changing, session.Phase);
		Assert.Equal(1000 + InstanceChangeDecision.ChangeTimeoutMs, session.ChangeDeadlineMs);

		session.AutomoveStarted = true;
		session.ChangeIssued = true;
		session.Clear();
		Assert.False(session.IsActive);
		Assert.Equal(0, session.Instance);
		Assert.False(session.AutomoveStarted);
		Assert.False(session.ChangeIssued);
	}

	[Fact]
	public void Enqueue_replaces_active_session_and_clears_automove_flag()
	{
		var session = new InstanceChangeSession();
		session.Enqueue(2, 813);
		session.EnterApproach();
		session.AutomoveStarted = true;

		session.Enqueue(3, 814);
		Assert.Equal(InstanceChangePhase.WaitLanded, session.Phase);
		Assert.Equal(3, session.Instance);
		Assert.Equal(814u, session.Territory);
		Assert.False(session.AutomoveStarted);
	}
}
