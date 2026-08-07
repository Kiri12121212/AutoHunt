#nullable enable

namespace HuntTrainAuto.Tests.Movement;

public sealed class MountDecisionTests
{
	[Theory]
	[InlineData(true, true)]
	[InlineData(false, false)]
	public void ShouldEnqueueIfEnabled(bool useMount, bool expected)
		=> Assert.Equal(expected, MountDecision.ShouldEnqueueIfEnabled(useMount));

	[Theory]
	[InlineData(false, true, true, false, true)]
	[InlineData(true, true, true, false, false)]
	[InlineData(false, false, true, false, false)]
	[InlineData(false, true, false, false, false)]
	[InlineData(false, true, true, true, false)]
	public void CanBeginMountAttempt(
		bool lifestreamBusy,
		bool screenReady,
		bool playerReady,
		bool instanceActive,
		bool expected)
		=> Assert.Equal(
			expected,
			MountDecision.CanBeginMountAttempt(lifestreamBusy, screenReady, playerReady, instanceActive));

	[Theory]
	[InlineData(true, 0, true)]
	[InlineData(true, 5, true)]
	[InlineData(false, -1, true)]
	[InlineData(false, 0, false)]
	[InlineData(false, 12, false)]
	public void IsMountCompleteOrSkipped(bool mounted, int mountConfig, bool expected)
		=> Assert.Equal(expected, MountDecision.IsMountCompleteOrSkipped(mounted, mountConfig));

	[Theory]
	[InlineData(true, false, true)]
	[InlineData(false, true, true)]
	[InlineData(true, true, true)]
	[InlineData(false, false, false)]
	public void NeedsTransitionWait(bool transition, bool casting, bool expected)
		=> Assert.Equal(expected, MountDecision.NeedsTransitionWait(transition, casting));

	[Theory]
	[InlineData(0u, true)]
	[InlineData(1u, false)]
	[InlineData(uint.MaxValue, false)]
	public void IsMountActionUsable(uint status, bool expected)
		=> Assert.Equal(expected, MountDecision.IsMountActionUsable(status));

	[Fact]
	public void ResolveMount_specific_unlocked()
	{
		var r = MountDecision.ResolveMount(42, configuredUnlocked: true, unlockedCount: 3, firstUnlockedId: 1);
		Assert.Equal(MountResolveKind.Specific, r.Kind);
		Assert.Equal(42, r.MountId);
		Assert.False(r.FellBack);
	}

	[Fact]
	public void ResolveMount_locked_single_unlock_falls_back_to_that()
	{
		var r = MountDecision.ResolveMount(99, configuredUnlocked: false, unlockedCount: 1, firstUnlockedId: 7);
		Assert.Equal(MountResolveKind.Specific, r.Kind);
		Assert.Equal(7, r.MountId);
		Assert.True(r.FellBack);
		Assert.Equal(99, r.RequestedMountId);
	}

	[Fact]
	public void ResolveMount_locked_multi_unlock_falls_back_to_random()
	{
		var r = MountDecision.ResolveMount(99, configuredUnlocked: false, unlockedCount: 4, firstUnlockedId: 7);
		Assert.Equal(MountResolveKind.Random, r.Kind);
		Assert.Equal(0, r.MountId);
		Assert.True(r.FellBack);
		Assert.Equal(99, r.RequestedMountId);
	}

	[Fact]
	public void ResolveMount_random_with_multi_stays_random()
	{
		var r = MountDecision.ResolveMount(0, configuredUnlocked: false, unlockedCount: 5, firstUnlockedId: 2);
		Assert.Equal(MountResolveKind.Random, r.Kind);
		Assert.False(r.FellBack);
	}

	[Fact]
	public void ResolveMount_random_with_single_uses_that_mount()
	{
		var r = MountDecision.ResolveMount(0, configuredUnlocked: false, unlockedCount: 1, firstUnlockedId: 3);
		Assert.Equal(MountResolveKind.Specific, r.Kind);
		Assert.Equal(3, r.MountId);
		Assert.False(r.FellBack);
	}

	[Fact]
	public void ResolveMount_none_unlocked_soft_skip()
	{
		var r = MountDecision.ResolveMount(5, configuredUnlocked: false, unlockedCount: 0, firstUnlockedId: 0);
		Assert.Equal(MountResolveKind.NoUnlocked, r.Kind);
		Assert.True(r.FellBack);
	}

	[Fact]
	public void DecideMountTick_done_when_mounted()
	{
		var r = MountDecision.DecideMountTick(
			mounted: true,
			mountConfig: 0,
			mountOrOrnamentTransition: false,
			casting: false,
			checkThrottleReady: true,
			mountActionUsable: true,
			resolve: RandomResolve(),
			animationLocked: false,
			summonThrottleReady: true);
		Assert.Equal(MountTickKind.Done, r.Kind);
	}

	[Fact]
	public void DecideMountTick_done_when_never_mount()
	{
		var r = MountDecision.DecideMountTick(
			mounted: false,
			mountConfig: MountDecision.NeverMount,
			mountOrOrnamentTransition: false,
			casting: false,
			checkThrottleReady: true,
			mountActionUsable: true,
			resolve: RandomResolve(),
			animationLocked: false,
			summonThrottleReady: true);
		Assert.Equal(MountTickKind.Done, r.Kind);
	}

	[Fact]
	public void DecideMountTick_waits_and_forces_throttle_during_transition()
	{
		var r = MountDecision.DecideMountTick(
			mounted: false,
			mountConfig: 0,
			mountOrOrnamentTransition: true,
			casting: false,
			checkThrottleReady: true,
			mountActionUsable: true,
			resolve: RandomResolve(),
			animationLocked: false,
			summonThrottleReady: true);
		Assert.Equal(MountTickKind.Wait, r.Kind);
		Assert.True(r.ForceCheckThrottle);
	}

	[Fact]
	public void DecideMountTick_waits_and_forces_throttle_while_casting()
	{
		var r = MountDecision.DecideMountTick(
			mounted: false,
			mountConfig: 0,
			mountOrOrnamentTransition: false,
			casting: true,
			checkThrottleReady: false,
			mountActionUsable: true,
			resolve: RandomResolve(),
			animationLocked: false,
			summonThrottleReady: true);
		Assert.Equal(MountTickKind.Wait, r.Kind);
		Assert.True(r.ForceCheckThrottle);
	}

	[Fact]
	public void DecideMountTick_waits_when_check_throttle_not_ready()
	{
		var r = MountDecision.DecideMountTick(
			mounted: false,
			mountConfig: 0,
			mountOrOrnamentTransition: false,
			casting: false,
			checkThrottleReady: false,
			mountActionUsable: true,
			resolve: RandomResolve(),
			animationLocked: false,
			summonThrottleReady: true);
		Assert.Equal(MountTickKind.Wait, r.Kind);
		Assert.False(r.ForceCheckThrottle);
	}

	[Fact]
	public void DecideMountTick_done_when_action_not_usable()
	{
		var r = MountDecision.DecideMountTick(
			mounted: false,
			mountConfig: 0,
			mountOrOrnamentTransition: false,
			casting: false,
			checkThrottleReady: true,
			mountActionUsable: false,
			resolve: RandomResolve(),
			animationLocked: false,
			summonThrottleReady: true);
		Assert.Equal(MountTickKind.Done, r.Kind);
	}

	[Fact]
	public void DecideMountTick_done_warn_when_no_unlocks()
	{
		var resolve = new MountResolveResult
		{
			Kind = MountResolveKind.NoUnlocked,
			FellBack = true,
			RequestedMountId = 9,
		};
		var r = MountDecision.DecideMountTick(
			mounted: false,
			mountConfig: 9,
			mountOrOrnamentTransition: false,
			casting: false,
			checkThrottleReady: true,
			mountActionUsable: true,
			resolve: resolve,
			animationLocked: false,
			summonThrottleReady: true);
		Assert.Equal(MountTickKind.Done, r.Kind);
		Assert.True(r.WarnNoMounts);
	}

	[Fact]
	public void DecideMountTick_waits_while_animation_locked()
	{
		var r = MountDecision.DecideMountTick(
			mounted: false,
			mountConfig: 0,
			mountOrOrnamentTransition: false,
			casting: false,
			checkThrottleReady: true,
			mountActionUsable: true,
			resolve: RandomResolve(),
			animationLocked: true,
			summonThrottleReady: true);
		Assert.Equal(MountTickKind.Wait, r.Kind);
	}

	[Fact]
	public void DecideMountTick_waits_when_summon_throttled()
	{
		var r = MountDecision.DecideMountTick(
			mounted: false,
			mountConfig: 0,
			mountOrOrnamentTransition: false,
			casting: false,
			checkThrottleReady: true,
			mountActionUsable: true,
			resolve: RandomResolve(),
			animationLocked: false,
			summonThrottleReady: false);
		Assert.Equal(MountTickKind.Wait, r.Kind);
	}

	[Fact]
	public void DecideMountTick_summon_random()
	{
		var r = MountDecision.DecideMountTick(
			mounted: false,
			mountConfig: 0,
			mountOrOrnamentTransition: false,
			casting: false,
			checkThrottleReady: true,
			mountActionUsable: true,
			resolve: RandomResolve(),
			animationLocked: false,
			summonThrottleReady: true);
		Assert.Equal(MountTickKind.SummonRandom, r.Kind);
	}

	[Fact]
	public void DecideMountTick_summon_specific_with_fallback_warn()
	{
		var resolve = new MountResolveResult
		{
			Kind = MountResolveKind.Specific,
			MountId = 7,
			FellBack = true,
			RequestedMountId = 99,
		};
		var r = MountDecision.DecideMountTick(
			mounted: false,
			mountConfig: 99,
			mountOrOrnamentTransition: false,
			casting: false,
			checkThrottleReady: true,
			mountActionUsable: true,
			resolve: resolve,
			animationLocked: false,
			summonThrottleReady: true);
		Assert.Equal(MountTickKind.SummonSpecific, r.Kind);
		Assert.Equal(7, r.SummonMountId);
		Assert.True(r.WarnFallback);
		Assert.Equal(99, r.RequestedMountId);
		Assert.Equal(7, r.FallbackMountId);
	}

	[Fact]
	public void CheckThrottle_force_extends_only()
	{
		Assert.True(MountDecision.IsCheckReady(0, 100));
		Assert.False(MountDecision.IsCheckReady(500, 100));
		Assert.Equal(2100, MountDecision.ForceCheckThrottle(0, 100));
		Assert.Equal(5000, MountDecision.ForceCheckThrottle(5000, 100));
	}

	[Fact]
	public void TryFireSummon_arms_cooldown()
	{
		long next = 0;
		Assert.True(MountDecision.TryFireSummon(ref next, nowMs: 1000));
		Assert.Equal(1000 + MountDecision.SummonCooldownMs, next);
		Assert.False(MountDecision.TryFireSummon(ref next, nowMs: 1000 + MountDecision.SummonCooldownMs - 1));
		Assert.True(MountDecision.TryFireSummon(ref next, nowMs: 1000 + MountDecision.SummonCooldownMs));
	}

	[Fact]
	public void SessionTimeout()
	{
		Assert.False(MountDecision.IsSessionTimedOut(0, 100));
		Assert.False(MountDecision.IsSessionTimedOut(200, 100));
		Assert.True(MountDecision.IsSessionTimedOut(200, 200));
		Assert.True(MountDecision.IsSessionTimedOut(200, 201));
	}

	[Fact]
	public void MountSession_enqueue_and_clear()
	{
		var s = new MountSession();
		Assert.False(s.IsActive);
		s.Enqueue(1000);
		Assert.Equal(MountPhase.WaitReady, s.Phase);
		Assert.Equal(0, s.DeadlineMs);
		s.EnterMounting(5000);
		Assert.Equal(MountPhase.Mounting, s.Phase);
		Assert.Equal(5000 + MountDecision.SessionTimeoutMs, s.DeadlineMs);
		s.Clear();
		Assert.False(s.IsActive);
		Assert.Equal(0, s.DeadlineMs);
	}

	[Fact]
	public void MountSession_deadline_starts_on_mounting_not_wait_ready()
	{
		var s = new MountSession();
		s.Enqueue(0);
		Assert.False(MountDecision.IsSessionTimedOut(s.DeadlineMs, 120_000));
		s.EnterMounting(10_000);
		Assert.False(MountDecision.IsSessionTimedOut(s.DeadlineMs, 10_000));
		Assert.True(MountDecision.IsSessionTimedOut(s.DeadlineMs, 10_000 + MountDecision.SessionTimeoutMs));
	}

	private static MountResolveResult RandomResolve() => new()
	{
		Kind = MountResolveKind.Random,
		MountId = 0,
	};
}
