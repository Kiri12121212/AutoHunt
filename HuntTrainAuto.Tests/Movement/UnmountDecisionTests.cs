#nullable enable

namespace HuntTrainAuto.Tests.Movement;

public sealed class UnmountDecisionTests
{
	[Theory]
	[InlineData(true, true, true, false, false, false)]
	[InlineData(true, true, true, true, true, true)]
	[InlineData(true, true, false, false, true, true)]
	[InlineData(true, true, false, false, false, false)]
	[InlineData(true, true, true, false, true, false)]
	[InlineData(true, false, true, false, true, false)]
	[InlineData(true, false, false, false, true, true)]
	[InlineData(false, true, true, false, true, false)]
	[InlineData(false, true, false, false, true, false)]
	public void ShouldFlagArrived(
		bool withinArrival,
		bool autoUnmount,
		bool mountedOrFlying,
		bool readyFollow,
		bool huntTargetFound,
		bool expected)
		=> Assert.Equal(
			expected,
			UnmountDecision.ShouldFlagArrived(
				withinArrival,
				autoUnmount,
				mountedOrFlying,
				readyFollow,
				huntTargetFound));

	[Theory]
	[InlineData(true, true)]
	[InlineData(false, false)]
	public void ShouldEnqueueIfEnabled(bool autoUnmount, bool expected)
		=> Assert.Equal(expected, UnmountDecision.ShouldEnqueueIfEnabled(autoUnmount));

	[Theory]
	[InlineData(true, false, true)]
	[InlineData(true, true, false)]
	[InlineData(false, false, false)]
	[InlineData(false, true, false)]
	public void ShouldStartUnmountJob(bool autoUnmount, bool alreadyActive, bool expected)
		=> Assert.Equal(
			expected,
			UnmountDecision.ShouldStartUnmountJob(autoUnmount, alreadyActive));

	[Theory]
	[InlineData(true, true, false, false, false)]
	[InlineData(true, true, false, true, true)]
	[InlineData(true, true, true, false, false)]
	[InlineData(true, false, false, false, false)]
	[InlineData(false, true, false, false, false)]
	public void ShouldEnqueueOnArrival(
		bool autoUnmount,
		bool isArrived,
		bool alreadyActive,
		bool huntTargetFound,
		bool expected)
		=> Assert.Equal(
			expected,
			UnmountDecision.ShouldEnqueueOnArrival(
				autoUnmount,
				isArrived,
				alreadyActive,
				huntTargetFound));

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
	[InlineData(true, true, true, true, false, true)] // leftover HasActive must not block (a6pb)
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
	[InlineData(false, false, true)]
	[InlineData(true, false, false)]
	[InlineData(false, true, false)]
	[InlineData(true, true, false)]
	public void IsUnmountCompleteOrSkipped(bool mounted, bool inFlight, bool expected)
		=> Assert.Equal(expected, UnmountDecision.IsUnmountCompleteOrSkipped(mounted, inFlight));

	[Theory]
	[InlineData(false, false, true)]
	[InlineData(true, false, false)]
	[InlineData(false, true, false)]
	public void ShouldEnqueueDivertUnmount(bool active, bool ready, bool expected)
		=> Assert.Equal(expected, UnmountDecision.ShouldEnqueueDivertUnmount(active, ready));

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
	public void DecideUnmountTick_not_done_while_in_flight_unmounted()
	{
		var r = UnmountDecision.DecideUnmountTick(
			mounted: false,
			mountOrOrnamentTransition: false,
			casting: false,
			checkThrottleReady: true,
			dismountActionUsable: true,
			animationLocked: false,
			dismountThrottleReady: true,
			inFlight: true);
		Assert.NotEqual(UnmountTickKind.Done, r.Kind);
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
		Assert.Equal(UnmountWaitReason.TransitionOrCasting, r.WaitReason);
		Assert.False(r.ForceCheckThrottle);
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
		Assert.Equal(UnmountWaitReason.TransitionOrCasting, r.WaitReason);
		Assert.False(r.ForceCheckThrottle);
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
	public void DecideUnmountTick_wait_when_action_unusable()
	{
		var r = UnmountDecision.DecideUnmountTick(
			mounted: true,
			mountOrOrnamentTransition: false,
			casting: false,
			checkThrottleReady: true,
			dismountActionUsable: false,
			animationLocked: false,
			dismountThrottleReady: true);
		Assert.Equal(UnmountTickKind.Wait, r.Kind);
		Assert.True(r.ForceCheckThrottle);
		Assert.Equal(UnmountDecision.ActionRetryCooldownMs, r.ForceCheckCooldownMs);
		Assert.False(r.ReadyForGroundFollow);
	}

	[Fact]
	public void DecideUnmountTick_wait_when_action_unusable_even_if_dismount_throttle_ready()
	{
		var r = UnmountDecision.DecideUnmountTick(
			mounted: true,
			mountOrOrnamentTransition: false,
			casting: false,
			checkThrottleReady: true,
			dismountActionUsable: false,
			animationLocked: false,
			dismountThrottleReady: false);
		Assert.Equal(UnmountTickKind.Wait, r.Kind);
		Assert.True(r.ForceCheckThrottle);
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

	[Theory]
	[InlineData(UnmountTickKind.Done, UnmountWaitReason.None, true, "done (ground follow ready)")]
	[InlineData(UnmountTickKind.Wait, UnmountWaitReason.ActionUnavailable, false, "wait (ActionUnavailable)")]
	[InlineData(UnmountTickKind.Dismount, UnmountWaitReason.None, false, "dismount")]
	public void Describe_reports_outcome_and_reason(
		UnmountTickKind kind,
		UnmountWaitReason waitReason,
		bool readyForGroundFollow,
		string expected)
		=> Assert.Equal(
			expected,
			UnmountDecision.Describe(new UnmountTickResult
			{
				Kind = kind,
				WaitReason = waitReason,
				ReadyForGroundFollow = readyForGroundFollow,
			}));

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
		Assert.Equal(1000 + UnmountDecision.WaitReadyTimeoutMs, s.DeadlineMs);

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
	public void UnmountSession_wait_ready_deadline_arms_on_enqueue()
	{
		var s = new UnmountSession();
		s.Enqueue(0);
		Assert.False(UnmountDecision.IsSessionTimedOut(s.DeadlineMs, UnmountDecision.WaitReadyTimeoutMs - 1));
		Assert.True(UnmountDecision.IsSessionTimedOut(s.DeadlineMs, UnmountDecision.WaitReadyTimeoutMs));

		s.EnterUnmounting(10_000);
		Assert.False(UnmountDecision.IsSessionTimedOut(s.DeadlineMs, 10_000));
		Assert.True(UnmountDecision.IsSessionTimedOut(s.DeadlineMs, 10_000 + UnmountDecision.SessionTimeoutMs));
	}

	[Fact]
	public void Dismount_general_action_id_is_dismount()
		=> Assert.Equal(23u, UnmountDecision.DismountGeneralActionId);
}
