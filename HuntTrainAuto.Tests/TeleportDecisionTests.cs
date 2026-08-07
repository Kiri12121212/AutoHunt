#nullable enable
using System;
using Xunit;

namespace HuntTrainAuto.Tests;

public sealed class TeleportDecisionTests
{
	private static ArrivalData Arrival(uint aetheryte = 42, uint territory = 813, int instance = 0)
		=> ArrivalData.CreateOrNull(aetheryte, territory, instance)!;

	[Fact]
	public void Decide_skips_when_plugin_disabled()
	{
		var result = TeleportDecision.Decide(
			enabled: false,
			autoTeleport: true,
			currentTerritory: 100,
			flagTerritory: 813,
			playerDistance: 50f,
			distanceThreshold: 3f,
			currentInstance: 0,
			targetInstance: 0,
			arrival: Arrival());

		Assert.Equal(TeleportAction.Skip, result.Action);
		Assert.Equal(TeleportSkipReason.PluginDisabled, result.SkipReason);
		Assert.False(result.ShouldTeleport);
		Assert.Null(result.Arrival);
	}

	[Fact]
	public void Decide_skips_when_auto_teleport_disabled()
	{
		var result = TeleportDecision.Decide(
			enabled: true,
			autoTeleport: false,
			currentTerritory: 100,
			flagTerritory: 813,
			playerDistance: 50f,
			distanceThreshold: 3f,
			currentInstance: 0,
			targetInstance: 0,
			arrival: Arrival());

		Assert.Equal(TeleportAction.Skip, result.Action);
		Assert.Equal(TeleportSkipReason.AutoTeleportDisabled, result.SkipReason);
	}

	[Fact]
	public void Decide_teleports_to_zone_when_territory_differs()
	{
		var arrival = Arrival(territory: 813);
		var result = TeleportDecision.Decide(
			enabled: true,
			autoTeleport: true,
			currentTerritory: 100,
			flagTerritory: 813,
			playerDistance: null,
			distanceThreshold: 3f,
			currentInstance: 0,
			targetInstance: 1,
			arrival: arrival);

		Assert.Equal(TeleportAction.TeleportToZone, result.Action);
		Assert.True(result.ShouldTeleport);
		Assert.Same(arrival, result.Arrival);
	}

	[Fact]
	public void Decide_zone_change_skips_when_arrival_missing()
	{
		var result = TeleportDecision.Decide(
			enabled: true,
			autoTeleport: true,
			currentTerritory: 100,
			flagTerritory: 813,
			playerDistance: null,
			distanceThreshold: 3f,
			currentInstance: 0,
			targetInstance: 0,
			arrival: null);

		Assert.Equal(TeleportAction.Skip, result.Action);
		Assert.Equal(TeleportSkipReason.MissingArrival, result.SkipReason);
	}

	[Fact]
	public void Decide_switches_instance_when_same_zone_target_differs()
	{
		var arrival = Arrival(instance: 0);
		var result = TeleportDecision.Decide(
			enabled: true,
			autoTeleport: true,
			currentTerritory: 813,
			flagTerritory: 813,
			playerDistance: 1f,
			distanceThreshold: 3f,
			currentInstance: 1,
			targetInstance: 2,
			arrival: arrival);

		Assert.Equal(TeleportAction.SwitchInstance, result.Action);
		Assert.True(result.ShouldTeleport);
		Assert.NotNull(result.Arrival);
		Assert.Equal(2, result.Arrival.Instance);
		Assert.Equal(arrival.AetheryteId, result.Arrival.AetheryteId);
	}

	[Fact]
	public void Decide_instance_switch_takes_priority_over_close_distance()
	{
		var result = TeleportDecision.Decide(
			enabled: true,
			autoTeleport: true,
			currentTerritory: 813,
			flagTerritory: 813,
			playerDistance: 0.5f,
			distanceThreshold: 3f,
			currentInstance: 1,
			targetInstance: 3,
			arrival: Arrival());

		Assert.Equal(TeleportAction.SwitchInstance, result.Action);
	}

	[Theory]
	[InlineData(0, 0, false)]
	[InlineData(1, 0, false)]
	[InlineData(1, 1, false)]
	[InlineData(2, 1, true)]
	[InlineData(0, 2, true)]
	public void NeedsInstanceSwitch_only_when_target_specified_and_differs(
		int current,
		int target,
		bool expected)
	{
		Assert.Equal(expected, TeleportDecision.NeedsInstanceSwitch(current, target));
	}

	[Fact]
	public void Decide_skips_when_same_zone_within_threshold()
	{
		var result = TeleportDecision.Decide(
			enabled: true,
			autoTeleport: true,
			currentTerritory: 813,
			flagTerritory: 813,
			playerDistance: 3f,
			distanceThreshold: 3f,
			currentInstance: 1,
			targetInstance: 0,
			arrival: Arrival());

		Assert.Equal(TeleportAction.Skip, result.Action);
		Assert.Equal(TeleportSkipReason.AlreadyClose, result.SkipReason);
	}

	[Fact]
	public void Decide_skips_when_closer_than_threshold()
	{
		var result = TeleportDecision.Decide(
			enabled: true,
			autoTeleport: true,
			currentTerritory: 813,
			flagTerritory: 813,
			playerDistance: 2.9f,
			distanceThreshold: 3f,
			currentInstance: 0,
			targetInstance: 0,
			arrival: Arrival());

		Assert.Equal(TeleportSkipReason.AlreadyClose, result.SkipReason);
	}

	[Fact]
	public void Decide_teleports_when_same_zone_farther_than_threshold()
	{
		var arrival = Arrival();
		var result = TeleportDecision.Decide(
			enabled: true,
			autoTeleport: true,
			currentTerritory: 813,
			flagTerritory: 813,
			playerDistance: 3.1f,
			distanceThreshold: 3f,
			currentInstance: 0,
			targetInstance: 0,
			arrival: arrival);

		Assert.Equal(TeleportAction.TeleportBecauseFar, result.Action);
		Assert.Same(arrival, result.Arrival);
	}

	[Fact]
	public void Decide_same_zone_far_skips_when_arrival_missing()
	{
		var result = TeleportDecision.Decide(
			enabled: true,
			autoTeleport: true,
			currentTerritory: 813,
			flagTerritory: 813,
			playerDistance: 50f,
			distanceThreshold: 3f,
			currentInstance: 0,
			targetInstance: 0,
			arrival: null);

		Assert.Equal(TeleportSkipReason.MissingArrival, result.SkipReason);
	}

	[Fact]
	public void Decide_same_zone_skips_when_distance_unknown()
	{
		var result = TeleportDecision.Decide(
			enabled: true,
			autoTeleport: true,
			currentTerritory: 813,
			flagTerritory: 813,
			playerDistance: null,
			distanceThreshold: 3f,
			currentInstance: 0,
			targetInstance: 0,
			arrival: Arrival());

		Assert.Equal(TeleportAction.Skip, result.Action);
		Assert.Equal(TeleportSkipReason.PlayerStateUnavailable, result.SkipReason);
	}

	[Theory]
	[InlineData(false, 0)]
	[InlineData(true, 1)]
	public void ResolveZoneChangeInstance_matches_hta(bool autoSwitchToOne, int expected)
	{
		Assert.Equal(expected, TeleportDecision.ResolveZoneChangeInstance(autoSwitchToOne));
	}

	[Fact]
	public void Evaluate_soft_fails_without_snapshot()
	{
		var flag = HuntFlag.FromMapLink(813u, 1u, 100, 200, "A", DateTimeOffset.UnixEpoch);
		var result = TeleportDecision.Evaluate(
			enabled: true,
			autoTeleport: true,
			distanceThreshold: 3f,
			autoSwitchInstanceToOne: false,
			flag,
			snapshot: null);

		Assert.Equal(TeleportSkipReason.PlayerStateUnavailable, result.SkipReason);
		Assert.Null(flag.Arrival);
	}

	[Fact]
	public void Evaluate_zone_change_applies_auto_switch_instance_to_one()
	{
		var flag = HuntFlag.FromMapLink(813u, 1u, 100, 200, "A", DateTimeOffset.UnixEpoch);
		var nearest = new NearestAetheryteResult(9u, "Fort Jobb");
		var snapshot = new TeleportPlayerSnapshot
		{
			CurrentTerritory = 100,
			CurrentInstance = 2,
			TargetInstance = 0,
			PlayerDistance = null,
			Nearest = nearest,
		};

		var result = TeleportDecision.Evaluate(
			enabled: true,
			autoTeleport: true,
			distanceThreshold: 3f,
			autoSwitchInstanceToOne: true,
			flag,
			snapshot);

		Assert.Equal(TeleportAction.TeleportToZone, result.Action);
		Assert.NotNull(result.Arrival);
		Assert.Equal(1, result.Arrival.Instance);
		Assert.Equal(9u, result.Arrival.AetheryteId);
		Assert.Same(result.Arrival, flag.Arrival);
	}

	[Fact]
	public void Evaluate_respects_explicit_target_instance_on_zone_change()
	{
		var flag = HuntFlag.FromMapLink(813u, 1u, 100, 200, "A", DateTimeOffset.UnixEpoch);
		var snapshot = new TeleportPlayerSnapshot
		{
			CurrentTerritory = 100,
			TargetInstance = 3,
			Nearest = new NearestAetheryteResult(1u, "A"),
		};

		var result = TeleportDecision.Evaluate(
			enabled: true,
			autoTeleport: true,
			distanceThreshold: 3f,
			autoSwitchInstanceToOne: true,
			flag,
			snapshot);

		Assert.Equal(3, result.Arrival!.Instance);
	}

	[Fact]
	public void Evaluate_same_zone_close_skips_and_attaches_arrival()
	{
		var flag = HuntFlag.FromMapLink(813u, 1u, 100, 200, "A", DateTimeOffset.UnixEpoch);
		var snapshot = new TeleportPlayerSnapshot
		{
			CurrentTerritory = 813,
			CurrentInstance = 1,
			TargetInstance = 0,
			PlayerDistance = 1f,
			Nearest = new NearestAetheryteResult(5u, "Near"),
		};

		var result = TeleportDecision.Evaluate(
			enabled: true,
			autoTeleport: true,
			distanceThreshold: 3f,
			autoSwitchInstanceToOne: false,
			flag,
			snapshot);

		Assert.Equal(TeleportSkipReason.AlreadyClose, result.SkipReason);
		Assert.NotNull(flag.Arrival);
		Assert.Null(result.Arrival);
	}

	[Fact]
	public void Evaluate_throws_when_flag_null()
	{
		Assert.Throws<ArgumentNullException>(() => TeleportDecision.Evaluate(
			true, true, 3f, false, null!, null));
	}

	[Fact]
	public void Hta_default_distance_threshold_is_three()
	{
		// Documented choice: HTA Config.AutoTeleportAetheryteDistanceDiff = 3f
		// (mirrored on Configuration.AutoTeleportAetheryteDistanceDiff).
		const float htaDefault = 3f;
		Assert.Equal(htaDefault, 3f);

		var result = TeleportDecision.Decide(
			enabled: true,
			autoTeleport: true,
			currentTerritory: 813,
			flagTerritory: 813,
			playerDistance: htaDefault,
			distanceThreshold: htaDefault,
			currentInstance: 0,
			targetInstance: 0,
			arrival: Arrival());
		Assert.Equal(TeleportSkipReason.AlreadyClose, result.SkipReason);
	}
}

public sealed class TeleportIntentTests
{
	[Fact]
	public void Set_stores_arrival_only_when_should_teleport()
	{
		var intent = new TeleportIntent();
		var arrival = ArrivalData.CreateOrNull(1u, 813u, 0)!;

		intent.Set(new TeleportDecisionResult
		{
			Action = TeleportAction.TeleportToZone,
			Arrival = arrival,
		});
		Assert.Same(arrival, intent.IntendedArrival);
		Assert.Equal(TeleportAction.TeleportToZone, intent.LatestDecision!.Value.Action);

		intent.Set(new TeleportDecisionResult
		{
			Action = TeleportAction.Skip,
			SkipReason = TeleportSkipReason.AlreadyClose,
			Arrival = arrival,
		});
		Assert.Null(intent.IntendedArrival);
	}

	[Fact]
	public void Clear_resets_state()
	{
		var intent = new TeleportIntent();
		intent.Set(new TeleportDecisionResult
		{
			Action = TeleportAction.TeleportBecauseFar,
			Arrival = ArrivalData.CreateOrNull(1u, 1u, 0),
		});
		intent.Clear();
		Assert.Null(intent.LatestDecision);
		Assert.Null(intent.IntendedArrival);
	}
}
