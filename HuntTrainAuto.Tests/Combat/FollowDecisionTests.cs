#nullable enable
using System.Numerics;

namespace HuntTrainAuto.Tests.Combat;

public sealed class FollowDecisionTests
{
	[Theory]
	[InlineData(0f, 3f, 3f)]
	[InlineData(-1f, 3f, 3f)]
	[InlineData(5f, 3f, 5f)]
	[InlineData(0f, 0f, FollowDecision.DefaultFollowDistance)]
	[InlineData(0f, -2f, FollowDecision.DefaultFollowDistance)]
	[InlineData(100f, 3f, FollowDecision.MaxFollowDistance)]
	[InlineData(0.1f, 3f, FollowDecision.MinFollowDistance)]
	public void ResolveFollowDistance(float requested, float defaultDistance, float expected)
		=> Assert.Equal(expected, FollowDecision.ResolveFollowDistance(requested, defaultDistance));

	[Fact]
	public void DefaultFollowDistance_matches_config_default()
		=> Assert.Equal(3f, FollowDecision.DefaultFollowDistance);

	[Theory]
	[InlineData(3f, 3f)]
	[InlineData(0.5f, FollowDecision.MinFollowDistance)]
	[InlineData(15f, FollowDecision.MaxFollowDistance)]
	[InlineData(0.1f, FollowDecision.MinFollowDistance)]
	[InlineData(0f, FollowDecision.MinFollowDistance)]
	[InlineData(-5f, FollowDecision.MinFollowDistance)]
	[InlineData(20f, FollowDecision.MaxFollowDistance)]
	[InlineData(float.NaN, FollowDecision.DefaultFollowDistance)]
	[InlineData(float.PositiveInfinity, FollowDecision.DefaultFollowDistance)]
	[InlineData(float.NegativeInfinity, FollowDecision.DefaultFollowDistance)]
	public void ClampFollowDistance(float input, float expected)
		=> Assert.Equal(expected, FollowDecision.ClampFollowDistance(input));

	[Fact]
	public void FollowDistance_bounds_are_documented()
	{
		Assert.Equal(0.5f, FollowDecision.MinFollowDistance);
		Assert.Equal(15f, FollowDecision.MaxFollowDistance);
		Assert.True(FollowDecision.MinFollowDistance > 0f);
		Assert.True(FollowDecision.DefaultFollowDistance >= FollowDecision.MinFollowDistance);
		Assert.True(FollowDecision.DefaultFollowDistance <= FollowDecision.MaxFollowDistance);
	}

	[Fact]
	public void PreferCanFly_is_false()
		=> Assert.False(FollowDecision.PreferCanFly);

	[Theory]
	[InlineData(3f, 3f, true)]
	[InlineData(3.1f, 3f, true)]
	[InlineData(2.9f, 3f, false)]
	[InlineData(0f, 0f, true)]
	public void ShouldRepath(float distance, float followDistance, bool expected)
		=> Assert.Equal(expected, FollowDecision.ShouldRepath(distance, followDistance));

	[Fact]
	public void Distance_matches_vector3()
	{
		var a = new Vector3(0, 0, 0);
		var b = new Vector3(3, 4, 0);
		Assert.Equal(5f, FollowDecision.Distance(a, b));
	}

	[Theory]
	[InlineData(true, true, true, true, true, true)]
	[InlineData(false, true, true, true, true, false)]
	[InlineData(true, false, true, true, true, false)]
	[InlineData(true, true, false, true, true, false)]
	[InlineData(true, true, true, false, true, false)]
	[InlineData(true, true, true, true, false, false)]
	public void CanRunFollow(
		bool followEnabled,
		bool pluginEnabled,
		bool playerAvailable,
		bool hasTarget,
		bool vnavAvailable,
		bool expected)
		=> Assert.Equal(
			expected,
			FollowDecision.CanRunFollow(
				followEnabled, pluginEnabled, playerAvailable, hasTarget, vnavAvailable));

	[Fact]
	public void DecideOnDisable_stops_path()
	{
		var r = FollowDecision.DecideOnDisable();
		Assert.Equal(FollowTickKind.StopPath, r.Kind);
		Assert.True(r.StopPath);
		Assert.False(r.MoveToTarget);
	}

	[Theory]
	[InlineData(true, true)]
	[InlineData(false, false)]
	public void ShouldInvalidatePathOnTargetChange(bool targetChanged, bool expected)
		=> Assert.Equal(expected, FollowDecision.ShouldInvalidatePathOnTargetChange(targetChanged));

	[Fact]
	public void DecideFollowTick_disable_when_follow_off()
	{
		var r = Decide(followEnabled: false);
		Assert.Equal(FollowTickKind.StopPath, r.Kind);
		Assert.True(r.StopPath);
		Assert.False(r.MoveToTarget);
	}

	[Fact]
	public void DecideFollowTick_disable_when_plugin_off()
	{
		var r = Decide(pluginEnabled: false);
		Assert.Equal(FollowTickKind.StopPath, r.Kind);
		Assert.True(r.StopPath);
	}

	[Fact]
	public void DecideFollowTick_wait_when_player_missing()
	{
		var r = Decide(playerAvailable: false, distance: 10f);
		Assert.Equal(FollowTickKind.Wait, r.Kind);
		Assert.False(r.StopPath);
		Assert.False(r.MoveToTarget);
	}

	[Fact]
	public void DecideFollowTick_wait_when_target_missing()
	{
		var r = Decide(hasTarget: false, distance: 10f);
		Assert.Equal(FollowTickKind.Wait, r.Kind);
	}

	[Fact]
	public void DecideFollowTick_wait_when_vnav_absent()
	{
		var r = Decide(vnavAvailable: false, distance: 10f);
		Assert.Equal(FollowTickKind.Wait, r.Kind);
	}

	[Fact]
	public void DecideFollowTick_wait_when_throttle_not_ready()
	{
		var r = Decide(throttleReady: false, distance: 10f);
		Assert.Equal(FollowTickKind.Wait, r.Kind);
		Assert.False(r.StopPath);
		Assert.False(r.MoveToTarget);
	}

	[Fact]
	public void DecideFollowTick_idle_when_within_distance()
	{
		var r = Decide(distance: 2.5f, followDistance: 3f);
		Assert.Equal(FollowTickKind.IdleWithinRange, r.Kind);
		Assert.True(r.StopPath);
		Assert.False(r.MoveToTarget);
	}

	[Fact]
	public void DecideFollowTick_within_range_stops_path()
	{
		// TASKS 5.6 / 5.3: distance < follow distance → stop active path
		var r = Decide(distance: 1f, followDistance: 3f);
		Assert.Equal(FollowTickKind.IdleWithinRange, r.Kind);
		Assert.True(r.StopPath);
		Assert.False(r.MoveToTarget);
	}

	[Fact]
	public void DecideFollowTick_idle_at_exact_boundary_is_repath()
	{
		// AD: distance >= followDistance → repath
		var r = Decide(distance: 3f, followDistance: 3f);
		Assert.Equal(FollowTickKind.Repath, r.Kind);
		Assert.True(r.StopPath);
		Assert.True(r.MoveToTarget);
	}

	[Fact]
	public void DecideFollowTick_repath_when_beyond_distance()
	{
		var r = Decide(distance: 5f, followDistance: 3f);
		Assert.Equal(FollowTickKind.Repath, r.Kind);
		Assert.True(r.StopPath);
		Assert.True(r.MoveToTarget);
	}

	[Fact]
	public void Update_throttle_helpers()
	{
		Assert.True(FollowDecision.IsUpdateReady(0, 100));
		Assert.False(FollowDecision.IsUpdateReady(500, 100));

		long next = 0;
		Assert.True(FollowDecision.TryFireUpdate(ref next, nowMs: 1000));
		Assert.Equal(1000 + FollowDecision.UpdateCooldownMs, next);
		Assert.False(FollowDecision.TryFireUpdate(ref next, nowMs: 1000 + FollowDecision.UpdateCooldownMs - 1));
		Assert.True(FollowDecision.TryFireUpdate(ref next, nowMs: 1000 + FollowDecision.UpdateCooldownMs));
	}

	[Fact]
	public void UpdateCooldownMs_is_fifty()
		=> Assert.Equal(50, FollowDecision.UpdateCooldownMs);

	[Fact]
	public void TryFireUpdate_custom_cooldown()
	{
		long next = 0;
		Assert.True(FollowDecision.TryFireUpdate(ref next, nowMs: 100, cooldownMs: 200));
		Assert.Equal(300, next);
	}

	private static FollowTickResult Decide(
		bool followEnabled = true,
		bool pluginEnabled = true,
		bool playerAvailable = true,
		bool hasTarget = true,
		bool vnavAvailable = true,
		bool throttleReady = true,
		float distance = 0f,
		float followDistance = 3f)
		=> FollowDecision.DecideFollowTick(
			followEnabled,
			pluginEnabled,
			playerAvailable,
			hasTarget,
			vnavAvailable,
			throttleReady,
			distance,
			followDistance);
}
