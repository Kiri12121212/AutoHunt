#nullable enable
using Xunit;

namespace HuntTrainAuto.Tests;

public sealed class UnmountDecisionTests
{
	[Theory]
	[InlineData(true, true)]
	[InlineData(false, false)]
	public void ShouldEnqueueIfEnabled(bool autoUnmount, bool expected)
		=> Assert.Equal(expected, UnmountDecision.ShouldEnqueueIfEnabled(autoUnmount));

	[Theory]
	[InlineData(true, true, false, true)]
	[InlineData(true, true, true, false)]
	[InlineData(true, false, false, false)]
	[InlineData(false, true, false, false)]
	public void ShouldEnqueueOnArrival(
		bool autoUnmount,
		bool isArrived,
		bool alreadyActive,
		bool expected)
		=> Assert.Equal(
			expected,
			UnmountDecision.ShouldEnqueueOnArrival(autoUnmount, isArrived, alreadyActive));

	[Theory]
	[InlineData(false, false, false, true)]
	[InlineData(true, true, false, true)]
	[InlineData(true, false, true, true)]
	[InlineData(true, false, false, false)]
	public void IsPathReadyForUnmount(
		bool pathRunning,
		bool arrivalSignaled,
		bool pathStopped,
		bool expected)
		=> Assert.Equal(
			expected,
			UnmountDecision.IsPathReadyForUnmount(pathRunning, arrivalSignaled, pathStopped));

	[Theory]
	[InlineData(true, true, true, false, false, true)]
	[InlineData(false, true, true, false, false, false)]
	[InlineData(true, false, true, false, false, false)]
	[InlineData(true, true, false, false, false, false)]
	[InlineData(true, true, true, true, false, false)]
	[InlineData(true, true, true, false, true, false)]
	public void CanBeginUnmountAttempt(
		bool pathReady,
		bool screenReady,
		bool playerReady,
		bool teleportActive,
		bool instanceActive,
		bool expected)
		=> Assert.Equal(
			expected,
			UnmountDecision.CanBeginUnmountAttempt(
				pathReady, screenReady, playerReady, teleportActive, instanceActive));

	[Theory]
	[InlineData(false, true)]
	[InlineData(true, false)]
	public void IsUnmountCompleteOrSkipped(bool mounted, bool expected)
		=> Assert.Equal(expected, UnmountDecision.IsUnmountCompleteOrSkipped(mounted));

	[Theory]
	[InlineData(true, false, true)]
	[InlineData(false, true, true)]
	[InlineData(true, true, true)]
	[InlineData(false, false, false)]
	public void NeedsTransitionWait(bool transition, bool casting, bool expected)
		=> Assert.Equal(expected, UnmountDecision.NeedsTransitionWait(transition, casting));

	[Theory]
	[InlineData(0u, true)]
	[InlineData(1u, false)]
	[InlineData(uint.MaxValue, false)]
	public void IsDismountActionUsable(uint status, bool expected)
		=> Assert.Equal(expected, UnmountDecision.IsDismountActionUsable(status));

	[Fact]
	public void PreferCanFlyForGroundFollow_is_false()
		=> Assert.False(UnmountDecision.PreferCanFlyForGroundFollow);

	[Fact]
	public void DecideUnmountTick_done_when_unmounted()
	{
		var r = UnmountDecision.DecideUnmountTick(
			mounted: false,
			mountOrOrnamentTransition: false,
			casting: false,
			checkThrottleReady: true,
			dismountActionUsable: true,
			animationLocked: false,
			dismountThrottleReady: true);
		Assert.Equal(UnmountTickKind.Done, r.Kind);
		Assert.True(r.ReadyForGroundFollow);
	}

	[Fact]
	public void DecideUnmountTick_wait_on_transition()
	{
		var r = UnmountDecision.DecideUnmountTick(
			mounted: true,
			mountOrOrnamentTransition: true,
			casting: false,
			checkThrottleReady: true,
			dismountActionUsable: true,
			animationLocked: false,
			dismountThrottleReady: true);
		Assert.Equal(UnmountTickKind.Wait, r.Kind);
		Assert.True(r.ForceCheckThrottle);
		Assert.False(r.ReadyForGroundFollow);
	}

	[Fact]
	public void DecideUnmountTick_wait_on_casting()
	{
		var r = UnmountDecision.DecideUnmountTick(
			mounted: true,
			mountOrOrnamentTransition: false,
			casting: true,
			checkThrottleReady: true,
			dismountActionUsable: true,
			animationLocked: false,
			dismountThrottleReady: true);
		Assert.Equal(UnmountTickKind.Wait, r.Kind);
		Assert.True(r.ForceCheckThrottle);
	}

	[Fact]
	public void DecideUnmountTick_wait_when_check_not_ready()
	{
		var r = UnmountDecision.DecideUnmountTick(
			mounted: true,
			mountOrOrnamentTransition: false,
			casting: false,
			checkThrottleReady: false,
			dismountActionUsable: true,
			animationLocked: false,
			dismountThrottleReady: true);
		Assert.Equal(UnmountTickKind.Wait, r.Kind);
		Assert.False(r.ForceCheckThrottle);
	}

	[Fact]
	public void DecideUnmountTick_done_when_action_unusable()
	{
		var r = UnmountDecision.DecideUnmountTick(
			mounted: true,
			mountOrOrnamentTransition: false,
			casting: false,
			checkThrottleReady: true,
			dismountActionUsable: false,
			animationLocked: false,
			dismountThrottleReady: true);
		Assert.Equal(UnmountTickKind.Done, r.Kind);
		Assert.False(r.ReadyForGroundFollow);
	}

	[Fact]
	public void DecideUnmountTick_wait_when_animation_locked()
	{
		var r = UnmountDecision.DecideUnmountTick(
			mounted: true,
			mountOrOrnamentTransition: false,
			casting: false,
			checkThrottleReady: true,
			dismountActionUsable: true,
			animationLocked: true,
			dismountThrottleReady: true);
		Assert.Equal(UnmountTickKind.Wait, r.Kind);
	}

	[Fact]
	public void DecideUnmountTick_wait_when_dismount_throttle_not_ready()
	{
		var r = UnmountDecision.DecideUnmountTick(
			mounted: true,
			mountOrOrnamentTransition: false,
			casting: false,
			checkThrottleReady: true,
			dismountActionUsable: true,
			animationLocked: false,
			dismountThrottleReady: false);
		Assert.Equal(UnmountTickKind.Wait, r.Kind);
	}

	[Fact]
	public void DecideUnmountTick_dismount_when_ready()
	{
		var r = UnmountDecision.DecideUnmountTick(
			mounted: true,
			mountOrOrnamentTransition: false,
			casting: false,
			checkThrottleReady: true,
			dismountActionUsable: true,
			animationLocked: false,
			dismountThrottleReady: true);
		Assert.Equal(UnmountTickKind.Dismount, r.Kind);
		Assert.False(r.ReadyForGroundFollow);
	}

	[Fact]
	public void Check_and_dismount_throttle_helpers()
	{
		Assert.True(UnmountDecision.IsCheckReady(0, 100));
		Assert.False(UnmountDecision.IsCheckReady(500, 100));
		Assert.Equal(2100, UnmountDecision.ForceCheckThrottle(0, 100));
		Assert.Equal(5000, UnmountDecision.ForceCheckThrottle(5000, 100));

		long next = 0;
		Assert.True(UnmountDecision.TryFireDismount(ref next, nowMs: 1000));
		Assert.Equal(1000 + UnmountDecision.DismountCooldownMs, next);
		Assert.False(UnmountDecision.TryFireDismount(ref next, nowMs: 1000 + UnmountDecision.DismountCooldownMs - 1));
		Assert.True(UnmountDecision.TryFireDismount(ref next, nowMs: 1000 + UnmountDecision.DismountCooldownMs));
	}

	[Fact]
	public void Session_timeout()
	{
		Assert.False(UnmountDecision.IsSessionTimedOut(0, 100));
		Assert.False(UnmountDecision.IsSessionTimedOut(200, 100));
		Assert.True(UnmountDecision.IsSessionTimedOut(200, 200));
		Assert.True(UnmountDecision.IsSessionTimedOut(200, 201));
	}

	[Fact]
	public void UnmountSession_enqueue_and_clear_keeps_ground_follow()
	{
		var s = new UnmountSession();
		Assert.False(s.IsActive);
		s.Enqueue(1000);
		Assert.Equal(UnmountPhase.WaitReady, s.Phase);
		Assert.True(s.IsActive);
		Assert.Equal(0, s.DeadlineMs);

		s.EnterUnmounting(5000);
		Assert.Equal(UnmountPhase.Unmounting, s.Phase);
		Assert.Equal(5000 + UnmountDecision.SessionTimeoutMs, s.DeadlineMs);

		s.MarkGroundFollowReady();
		Assert.True(s.ReadyForGroundFollow);

		s.Clear();
		Assert.False(s.IsActive);
		Assert.True(s.ReadyForGroundFollow);

		s.ClearAll();
		Assert.False(s.ReadyForGroundFollow);
	}

	[Fact]
	public void UnmountSession_deadline_starts_on_unmounting_not_wait_ready()
	{
		var s = new UnmountSession();
		s.Enqueue(0);
		Assert.False(UnmountDecision.IsSessionTimedOut(s.DeadlineMs, 120_000));

		s.EnterUnmounting(10_000);
		Assert.False(UnmountDecision.IsSessionTimedOut(s.DeadlineMs, 10_000));
		Assert.True(UnmountDecision.IsSessionTimedOut(s.DeadlineMs, 10_000 + UnmountDecision.SessionTimeoutMs));
	}

	[Fact]
	public void Dismount_general_action_id_is_one()
		=> Assert.Equal(1u, UnmountDecision.DismountGeneralActionId);
}
