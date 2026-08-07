#nullable enable

namespace HuntTrainAuto.Tests.Teleport;

public sealed class TeleportThrottleTests
{
	[Fact]
	public void IsReady_false_before_deadline()
	{
		Assert.False(TeleportThrottle.IsReady(nextAllowedMs: 1000, nowMs: 999));
	}

	[Fact]
	public void IsReady_true_at_or_after_deadline()
	{
		Assert.True(TeleportThrottle.IsReady(1000, 1000));
		Assert.True(TeleportThrottle.IsReady(1000, 1500));
	}

	[Fact]
	public void TryFire_arms_cooldown_and_blocks_until_ready()
	{
		long next = 0;
		Assert.True(TeleportThrottle.TryFire(ref next, nowMs: 1000, cooldownMs: 500));
		Assert.Equal(1500, next);
		Assert.False(TeleportThrottle.TryFire(ref next, nowMs: 1499, cooldownMs: 500));
		Assert.True(TeleportThrottle.TryFire(ref next, nowMs: 1500, cooldownMs: 500));
		Assert.Equal(2000, next);
	}

	[Fact]
	public void SoftWait_teleport_cast_uses_2000_when_not_condition_casting()
	{
		var next = TeleportThrottle.SoftWaitNextAllowed(
			nextAllowedMs: 0,
			nowMs: 100,
			isCasting: true,
			castActionId: TeleportThrottle.TeleportCastActionId,
			conditionCasting: false,
			mountOrOrnamentTransition: false);
		Assert.Equal(100 + TeleportThrottle.PostTeleportCastCooldownMs, next);
	}

	[Fact]
	public void SoftWait_teleport_cast_uses_500_while_condition_casting()
	{
		var next = TeleportThrottle.SoftWaitNextAllowed(
			nextAllowedMs: 0,
			nowMs: 100,
			isCasting: true,
			castActionId: 5,
			conditionCasting: true,
			mountOrOrnamentTransition: false);
		Assert.Equal(100 + TeleportThrottle.CastingTeleportCooldownMs, next);
	}

	[Fact]
	public void SoftWait_other_cast_or_mount_uses_500()
	{
		Assert.Equal(
			600,
			TeleportThrottle.SoftWaitNextAllowed(0, 100, true, castActionId: 7, false, false));
		Assert.Equal(
			600,
			TeleportThrottle.SoftWaitNextAllowed(0, 100, false, 0, false, mountOrOrnamentTransition: true));
	}

	[Fact]
	public void SoftWait_null_when_idle()
	{
		Assert.Null(TeleportThrottle.SoftWaitNextAllowed(100, 100, false, 0, false, false));
	}

	[Fact]
	public void SoftWait_keeps_longer_existing_deadline()
	{
		var next = TeleportThrottle.SoftWaitNextAllowed(
			nextAllowedMs: 5000,
			nowMs: 100,
			isCasting: true,
			castActionId: 7,
			conditionCasting: false,
			mountOrOrnamentTransition: false);
		Assert.Equal(5000, next);
	}

	[Fact]
	public void SoftWait_extends_when_existing_deadline_shorter()
	{
		var next = TeleportThrottle.SoftWaitNextAllowed(
			nextAllowedMs: 200,
			nowMs: 100,
			isCasting: true,
			castActionId: TeleportThrottle.TeleportCastActionId,
			conditionCasting: false,
			mountOrOrnamentTransition: false);
		Assert.Equal(100 + TeleportThrottle.PostTeleportCastCooldownMs, next);
	}

	[Fact]
	public void ApplyPreDelay_extends_when_remaining_shorter()
	{
		var next = TeleportThrottle.ApplyPreDelay(
			nextAllowedMs: 100,
			nowMs: 100,
			enabled: true,
			delayMinMs: 200,
			delayMaxMs: 700,
			randomOffset: 50);
		Assert.Equal(350, next);
	}

	[Fact]
	public void ApplyPreDelay_keeps_longer_existing_throttle()
	{
		var next = TeleportThrottle.ApplyPreDelay(
			nextAllowedMs: 1000,
			nowMs: 100,
			enabled: true,
			delayMinMs: 200,
			delayMaxMs: 700,
			randomOffset: 0);
		Assert.Equal(1000, next);
	}

	[Fact]
	public void ApplyPreDelay_noop_when_disabled()
	{
		Assert.Equal(
			0,
			TeleportThrottle.ApplyPreDelay(0, 100, enabled: false, 200, 700, 50));
	}
}

public sealed class TeleportGateTests
{
	[Theory]
	[InlineData(false, false, false, false, true)]
	[InlineData(true, false, false, false, false)]
	[InlineData(false, true, false, false, false)]
	[InlineData(false, false, true, false, false)]
	[InlineData(false, false, false, true, false)]
	public void IsScreenReady(bool between, bool between51, bool cutscene, bool watching, bool expected)
		=> Assert.Equal(expected, TeleportGate.IsScreenReady(between, between51, cutscene, watching));

	[Fact]
	public void CanAttemptTeleport_requires_all_clear()
	{
		Assert.True(TeleportGate.CanAttemptTeleport(false, false, false, false, false));
		Assert.False(TeleportGate.CanAttemptTeleport(inCombat: true, false, false, false, false));
		Assert.False(TeleportGate.CanAttemptTeleport(false, betweenAreas: true, false, false, false));
		Assert.False(TeleportGate.CanAttemptTeleport(false, false, betweenAreas51: true, false, false));
		Assert.False(TeleportGate.CanAttemptTeleport(false, false, false, casting: true, false));
		Assert.False(TeleportGate.CanAttemptTeleport(false, false, false, false, isMoving: true));
		Assert.False(TeleportGate.CanAttemptTeleport(false, false, false, false, false, animationLocked: true));
	}

	[Fact]
	public void IsPlayerReady_and_auto_guards()
	{
		Assert.True(TeleportGate.IsPlayerReady(true, true, unconscious: false));
		Assert.False(TeleportGate.IsPlayerReady(false, true, false));
		Assert.False(TeleportGate.IsPlayerReady(true, false, false));
		Assert.False(TeleportGate.IsPlayerReady(true, true, unconscious: true));
		Assert.True(TeleportGate.IsAutoTeleportEnabled(true, true));
		Assert.False(TeleportGate.IsAutoTeleportEnabled(false, true));
		Assert.False(TeleportGate.IsAutoTeleportEnabled(true, false));
	}

	[Fact]
	public void Instance_enqueue_and_between_areas()
	{
		Assert.True(TeleportGate.ShouldEnqueueInstanceChange(1));
		Assert.False(TeleportGate.ShouldEnqueueInstanceChange(0));
		Assert.True(TeleportGate.IsBetweenAreas(true, false));
		Assert.True(TeleportGate.IsBetweenAreas(false, true));
		Assert.False(TeleportGate.IsBetweenAreas(false, false));
	}
}

public sealed class TeleportPlanTests
{
	[Fact]
	public void TryAdoptFromIntent_sets_active_from_intended_arrival()
	{
		var intent = new TeleportIntent();
		var arrival = ArrivalData.CreateOrNull(10u, 813u, 2)!;
		intent.Set(new TeleportDecisionResult
		{
			Action = TeleportAction.TeleportToZone,
			Arrival = arrival,
		});

		var plan = new TeleportPlan();
		Assert.True(plan.TryAdoptFromIntent(intent));
		Assert.Same(arrival, plan.Active);
		plan.Clear();
		Assert.Null(plan.Active);
	}

	[Fact]
	public void TryAdoptFromIntent_false_when_skip()
	{
		var intent = new TeleportIntent();
		intent.Set(new TeleportDecisionResult
		{
			Action = TeleportAction.Skip,
			SkipReason = TeleportSkipReason.AlreadyClose,
		});

		var plan = new TeleportPlan();
		Assert.False(plan.TryAdoptFromIntent(intent));
		Assert.Null(plan.Active);
	}

	[Fact]
	public void TryAdoptFromIntent_clears_stale_plan_on_skip()
	{
		var prior = ArrivalData.CreateOrNull(10u, 813u, 2)!;
		var plan = new TeleportPlan();
		plan.Set(prior);
		Assert.Same(prior, plan.Active);

		var intent = new TeleportIntent();
		intent.Set(new TeleportDecisionResult
		{
			Action = TeleportAction.Skip,
			SkipReason = TeleportSkipReason.AlreadyClose,
		});

		Assert.False(plan.TryAdoptFromIntent(intent));
		Assert.Null(plan.Active);
	}
}
