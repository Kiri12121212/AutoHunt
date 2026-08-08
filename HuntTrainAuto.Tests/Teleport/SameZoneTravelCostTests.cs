#nullable enable
using System.Numerics;

namespace HuntTrainAuto.Tests.Teleport;

public sealed class SameZoneTravelCostTests
{
	private static SameZoneTimeAwareSettings Settings(
		float cast = 5f,
		float load = 8f,
		float speed = 20f,
		float mountUp = 0f,
		float preDelay = 0f,
		bool retainFloor = true)
		=> new()
		{
			Enabled = true,
			PreDelaySeconds = preDelay,
			CastSeconds = cast,
			LoadEstimateSeconds = load,
			MountSpeedYalmsPerSec = speed,
			MountUpSeconds = mountUp,
			RetainDistanceAsFloor = retainFloor,
		};

	[Fact]
	public void PathLength_sums_segments()
	{
		var pts = new[]
		{
			new Vector3(0, 0, 0),
			new Vector3(3, 0, 4),
			new Vector3(3, 0, 10),
		};
		Assert.Equal(11f, SameZoneTravelCost.PathLength(pts));
	}

	[Fact]
	public void PathLength_empty_or_single_is_zero()
	{
		Assert.Equal(0f, SameZoneTravelCost.PathLength(System.Array.Empty<Vector3>()));
		Assert.Equal(0f, SameZoneTravelCost.PathLength([new Vector3(1, 2, 3)]));
	}

	[Fact]
	public void RideSeconds_divides_by_speed_plus_mount_up()
	{
		Assert.Equal(10f, SameZoneTravelCost.RideSeconds(200f, 20f));
		Assert.Equal(11.5f, SameZoneTravelCost.RideSeconds(200f, 20f, mountUpSeconds: 1.5f));
	}

	[Fact]
	public void TeleportTotalSeconds_adds_overhead_and_aetheryte_ride()
	{
		// pre 0.5 + cast 5 + load 8 + ride(100/20=5) = 18.5
		var t = SameZoneTravelCost.TeleportTotalSeconds(0.5f, 5f, 8f, 100f, 20f);
		Assert.Equal(18.5f, t);
	}

	[Fact]
	public void PreferDirect_when_direct_not_slower()
	{
		Assert.True(SameZoneTravelCost.PreferDirect(10f, 10f));
		Assert.True(SameZoneTravelCost.PreferDirect(9f, 10f));
		Assert.False(SameZoneTravelCost.PreferDirect(11f, 10f));
	}

	[Fact]
	public void ShouldSkipTeleportForTime_skips_when_direct_faster()
	{
		// t_direct = 100/20 = 5; t_tp = 5+8+10/20 = 13.5 → skip
		var estimate = new SameZoneTravelEstimate
		{
			Status = SameZonePathCostStatus.Ready,
			DirectPathLengthYalms = 100f,
			AetherytePathLengthYalms = 10f,
		};
		Assert.True(SameZoneTravelCost.ShouldSkipTeleportForTime(estimate, Settings()));
	}

	[Fact]
	public void ShouldSkipTeleportForTime_tps_when_direct_slower()
	{
		// t_direct = 800/20 = 40; t_tp = 5+8+10/20 = 13.5 → TP
		var estimate = new SameZoneTravelEstimate
		{
			Status = SameZonePathCostStatus.Ready,
			DirectPathLengthYalms = 800f,
			AetherytePathLengthYalms = 10f,
		};
		Assert.False(SameZoneTravelCost.ShouldSkipTeleportForTime(estimate, Settings()));
	}

	[Fact]
	public void ShouldSkipTeleportForTime_null_when_unavailable_or_pending()
	{
		var settings = Settings();
		Assert.Null(SameZoneTravelCost.ShouldSkipTeleportForTime(
			new SameZoneTravelEstimate { Status = SameZonePathCostStatus.Unavailable },
			settings));
		Assert.Null(SameZoneTravelCost.ShouldSkipTeleportForTime(
			new SameZoneTravelEstimate { Status = SameZonePathCostStatus.Pending },
			settings));
	}

	[Fact]
	public void PreDelaySecondsFromMs_averages_when_enabled()
	{
		Assert.Equal(0f, SameZoneTravelCost.PreDelaySecondsFromMs(false, 200, 700));
		Assert.Equal(0.45f, SameZoneTravelCost.PreDelaySecondsFromMs(true, 200, 700));
	}

	[Fact]
	public void CreateSettings_maps_config_fields()
	{
		var s = SameZoneTravelCost.CreateSettings(
			enabled: true,
			castSeconds: 5f,
			loadEstimateSeconds: 8f,
			mountSpeedYalmsPerSec: 20f,
			mountUpSeconds: 1.5f,
			retainDistanceAsFloor: true,
			teleportDelayEnabled: true,
			teleportDelayMinMs: 200,
			teleportDelayMaxMs: 700);
		Assert.True(s.Enabled);
		Assert.Equal(0.45f, s.PreDelaySeconds);
		Assert.Equal(1.5f, s.MountUpSeconds);
	}

	[Fact]
	public void CreateSettings_allows_zero_cast_estimate()
	{
		var s = SameZoneTravelCost.CreateSettings(
			enabled: true,
			castSeconds: 0f,
			loadEstimateSeconds: 0f,
			mountSpeedYalmsPerSec: 20f,
			mountUpSeconds: 0f,
			retainDistanceAsFloor: true,
			teleportDelayEnabled: false,
			teleportDelayMinMs: 0,
			teleportDelayMaxMs: 0);
		Assert.Equal(0f, s.CastSeconds);
		Assert.Equal(0f, s.LoadEstimateSeconds);
	}
}

public sealed class TeleportDecisionTimeAwareTests
{
	private const float YalmThreshold = 150f;

	private static ArrivalData Arrival() => ArrivalData.CreateOrNull(42u, 813u, 0)!;

	private static SameZoneTimeAwareSettings Aware(bool retainFloor = true) => new()
	{
		Enabled = true,
		PreDelaySeconds = 0f,
		CastSeconds = 5f,
		LoadEstimateSeconds = 8f,
		MountSpeedYalmsPerSec = 20f,
		MountUpSeconds = 0f,
		RetainDistanceAsFloor = retainFloor,
	};

	[Fact]
	public void Decide_time_aware_skips_when_injected_direct_faster()
	{
		var estimate = new SameZoneTravelEstimate
		{
			Status = SameZonePathCostStatus.Ready,
			DirectPathLengthYalms = 100f,
			AetherytePathLengthYalms = 10f,
		};
		var result = TeleportDecision.Decide(
			true, true, 813, 813, 400f, YalmThreshold, 0, 0, Arrival(),
			Aware(), estimate);
		Assert.Equal(TeleportSkipReason.AlreadyClose, result.SkipReason);
		Assert.False(result.ShouldTeleport);
	}

	[Fact]
	public void Decide_time_aware_tps_when_injected_direct_slower()
	{
		var arrival = Arrival();
		var estimate = new SameZoneTravelEstimate
		{
			Status = SameZonePathCostStatus.Ready,
			DirectPathLengthYalms = 800f,
			AetherytePathLengthYalms = 10f,
		};
		var result = TeleportDecision.Decide(
			true, true, 813, 813, 400f, YalmThreshold, 0, 0, arrival,
			Aware(), estimate);
		Assert.Equal(TeleportAction.TeleportBecauseFar, result.Action);
		Assert.Same(arrival, result.Arrival);
	}

	[Fact]
	public void Decide_time_aware_pending_defers()
	{
		var result = TeleportDecision.Decide(
			true, true, 813, 813, 400f, YalmThreshold, 0, 0, Arrival(),
			Aware(),
			new SameZoneTravelEstimate { Status = SameZonePathCostStatus.Pending });
		Assert.Equal(TeleportSkipReason.AwaitingTravelCost, result.SkipReason);
		Assert.False(result.ShouldTeleport);
	}

	[Fact]
	public void Decide_time_aware_unavailable_soft_falls_to_yalm_distance()
	{
		var arrival = Arrival();
		var far = TeleportDecision.Decide(
			true, true, 813, 813, 400f, YalmThreshold, 0, 0, arrival,
			Aware(),
			new SameZoneTravelEstimate { Status = SameZonePathCostStatus.Unavailable });
		Assert.Equal(TeleportAction.TeleportBecauseFar, far.Action);

		var close = TeleportDecision.Decide(
			true, true, 813, 813, 40f, YalmThreshold, 0, 0, arrival,
			Aware(),
			new SameZoneTravelEstimate { Status = SameZonePathCostStatus.Unavailable });
		Assert.Equal(TeleportSkipReason.AlreadyClose, close.SkipReason);
	}

	[Fact]
	public void Decide_time_aware_instance_switch_beats_distance_floor()
	{
		var result = TeleportDecision.Decide(
			true, true, 813, 813, 40f, YalmThreshold, 1, 2, Arrival(),
			Aware(retainFloor: true),
			new SameZoneTravelEstimate
			{
				Status = SameZonePathCostStatus.Ready,
				DirectPathLengthYalms = 800f,
				AetherytePathLengthYalms = 10f,
			});
		Assert.Equal(TeleportAction.SwitchInstance, result.Action);
		Assert.Equal(2, result.Arrival!.Instance);
	}

	[Fact]
	public void Decide_cross_zone_ignores_time_aware()
	{
		var arrival = Arrival();
		var result = TeleportDecision.Decide(
			true, true, 100, 813, 40f, YalmThreshold, 0, 0, arrival,
			Aware(),
			new SameZoneTravelEstimate
			{
				Status = SameZonePathCostStatus.Ready,
				DirectPathLengthYalms = 1f,
				AetherytePathLengthYalms = 1f,
			});
		Assert.Equal(TeleportAction.TeleportToZone, result.Action);
	}

	[Fact]
	public void Evaluate_passes_travel_estimate_from_snapshot()
	{
		var flag = HuntFlag.FromMapLink(813u, 1u, 100, 200, "A", System.DateTimeOffset.UnixEpoch);
		var snapshot = new TeleportPlayerSnapshot
		{
			CurrentTerritory = 813,
			PlayerDistance = 400f,
			Nearest = new NearestAetheryteResult(5u, "Near", 10f, 12f),
			TravelEstimate = new SameZoneTravelEstimate
			{
				Status = SameZonePathCostStatus.Ready,
				DirectPathLengthYalms = 80f,
				AetherytePathLengthYalms = 20f,
			},
		};
		var result = TeleportDecision.Evaluate(
			true, true, YalmThreshold, false, flag, snapshot, Aware());
		Assert.Equal(TeleportSkipReason.AlreadyClose, result.SkipReason);
	}
}
