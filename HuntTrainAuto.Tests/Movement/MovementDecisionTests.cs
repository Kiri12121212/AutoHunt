#nullable enable
using System.Numerics;

namespace HuntTrainAuto.Tests.Movement;

public sealed class MovementDecisionTests
{
	[Theory]
	[InlineData(1u, true)]
	[InlineData(49u, true)]
	[InlineData(47u, true)]
	[InlineData(0u, false)]
	[InlineData(2u, false)]
	[InlineData(48u, false)]
	public void IsFlyingTerritoryUse(uint use, bool expected)
		=> Assert.Equal(expected, MovementDecision.IsFlyingTerritoryUse(use));

	[Theory]
	[InlineData(813u, 1u, true, true)]
	[InlineData(813u, 49u, true, true)]
	[InlineData(813u, 47u, true, true)]
	[InlineData(0u, 1u, true, false)]
	[InlineData(813u, 1u, false, false)]
	[InlineData(813u, 2u, true, false)]
	public void ZoneSupportsFlying(uint territory, uint use, bool aetherDone, bool expected)
		=> Assert.Equal(expected, MovementDecision.ZoneSupportsFlying(territory, use, aetherDone));

	[Theory]
	[InlineData(true, true, true)]
	[InlineData(true, false, false)]
	[InlineData(false, true, false)]
	[InlineData(false, false, false)]
	public void ResolveFly(bool requested, bool zoneOk, bool expected)
		=> Assert.Equal(expected, MovementDecision.ResolveFly(requested, zoneOk));

	[Theory]
	[InlineData(true, true, true)]
	[InlineData(true, false, false)]
	[InlineData(false, true, false)]
	public void PreferFlyWhenMounted(bool mounted, bool zoneOk, bool expected)
		=> Assert.Equal(expected, MovementDecision.PreferFlyWhenMounted(mounted, zoneOk));

	[Fact]
	public void IsArrived_zero_destination()
		=> Assert.True(MovementDecision.IsArrived(Vector3.Zero, 100f, 0.25f, useMesh: true));

	[Fact]
	public void IsArrived_within_tolerance_mesh()
	{
		Assert.True(MovementDecision.IsArrived(new Vector3(1, 0, 0), 0.2f, 0.25f, useMesh: true));
		Assert.False(MovementDecision.IsArrived(new Vector3(1, 0, 0), 0.3f, 0.25f, useMesh: true));
	}

	[Fact]
	public void IsArrived_direct_path_subtracts_one_yalm()
	{
		// distance 1.2 → adjusted 0.2 ≤ 0.25
		Assert.True(MovementDecision.IsArrived(new Vector3(1, 0, 0), 1.2f, 0.25f, useMesh: false));
		Assert.False(MovementDecision.IsArrived(new Vector3(1, 0, 0), 1.2f, 0.25f, useMesh: true));
	}

	[Theory]
	[InlineData(true, true, false, false, true)]
	[InlineData(true, false, false, false, false)]
	[InlineData(true, true, true, false, false)]
	[InlineData(true, true, false, true, false)]
	[InlineData(false, true, false, false, false)]
	public void NeedsTakeoff(bool fly, bool mounted, bool inFlight, bool casting, bool expected)
		=> Assert.Equal(expected, MovementDecision.NeedsTakeoff(fly, mounted, inFlight, casting));

	[Theory]
	[InlineData(1, true)]
	[InlineData(0, false)]
	[InlineData(2, false)]
	public void ShouldSetLastPointTolerance(int waypoints, bool expected)
		=> Assert.Equal(expected, MovementDecision.ShouldSetLastPointTolerance(waypoints));

	[Theory]
	[InlineData(true, true, false, 0, false, false)]
	[InlineData(false, true, false, 0, false, true)]
	[InlineData(true, false, false, 0, false, true)]
	[InlineData(true, true, true, 0, false, true)]
	[InlineData(true, true, false, 1, false, true)]
	[InlineData(true, true, false, 3, false, true)]
	[InlineData(true, true, false, 0, true, true)]
	public void ShouldWaitBeforeMeshPathfind(
		bool playerReady,
		bool navReady,
		bool pathfindInProgress,
		int numWaypoints,
		bool pathIsRunning,
		bool expectedWait)
		=> Assert.Equal(
			expectedWait,
			MovementDecision.ShouldWaitBeforeMeshPathfind(
				playerReady, navReady, pathfindInProgress, numWaypoints, pathIsRunning));

	[Fact]
	public void CanStartMeshPathfind_when_idle_and_ready()
		=> Assert.True(MovementDecision.CanStartMeshPathfind(
			playerReady: true,
			navReady: true,
			pathfindInProgress: false,
			numWaypoints: 0,
			pathIsRunning: false));

	[Fact]
	public void DecideMoveTick_waits_mesh_when_path_running()
	{
		var r = Decide(distance: 50f, pathIsRunning: true, numWaypoints: 0);
		Assert.Equal(MoveTickKind.Wait, r.Kind);
	}

	[Fact]
	public void DecideMoveTick_invalid_player()
	{
		var r = Decide(playerValid: false);
		Assert.Equal(MoveTickKind.WaitPlayerInvalid, r.Kind);
	}

	[Fact]
	public void DecideMoveTick_forces_ground_when_zone_no_fly()
	{
		var r = Decide(flyRequested: true, zoneSupportsFlying: false, distance: 50f);
		Assert.Equal(MoveTickKind.StartMeshPath, r.Kind);
		Assert.False(r.Fly);
	}

	[Fact]
	public void DecideMoveTick_takeoff_when_mounted_fly_not_in_flight()
	{
		var r = Decide(
			flyRequested: true,
			zoneSupportsFlying: true,
			mounted: true,
			inFlight: false,
			casting: false);
		Assert.Equal(MoveTickKind.Takeoff, r.Kind);
		Assert.True(r.Fly);
	}

	[Fact]
	public void DecideMoveTick_grounds_when_fly_requested_unmounted()
	{
		var r = Decide(
			flyRequested: true,
			zoneSupportsFlying: true,
			mounted: false,
			inFlight: false,
			distance: 50f);
		Assert.Equal(MoveTickKind.StartMeshPath, r.Kind);
		Assert.False(r.Fly);
	}

	[Fact]
	public void DecideMoveTick_waits_when_casting_during_needed_takeoff()
	{
		var r = Decide(
			flyRequested: true,
			zoneSupportsFlying: true,
			mounted: true,
			inFlight: false,
			casting: true);
		Assert.Equal(MoveTickKind.Wait, r.Kind);
		Assert.True(r.Fly);
	}

	[Fact]
	public void DecideMoveTick_arrived_stops_path()
	{
		var r = Decide(distance: 0.1f, lastPointTolerance: 0.25f, destination: new Vector3(5, 0, 0));
		Assert.Equal(MoveTickKind.Arrived, r.Kind);
		Assert.True(r.StopPath);
	}

	[Fact]
	public void DecideMoveTick_zero_destination_arrived_without_stop()
	{
		var r = Decide(destination: Vector3.Zero, distance: 99f);
		Assert.Equal(MoveTickKind.Arrived, r.Kind);
		Assert.False(r.StopPath);
	}

	[Fact]
	public void DecideMoveTick_last_waypoint_sets_tolerance()
	{
		var r = Decide(
			flyRequested: false,
			inFlight: false,
			distance: 10f,
			numWaypoints: 1,
			playerReady: true,
			navReady: true);
		Assert.Equal(MoveTickKind.SetLastPointToleranceAndWait, r.Kind);
	}

	[Fact]
	public void DecideMoveTick_waits_while_pathfind_in_progress()
	{
		var r = Decide(
			distance: 10f,
			pathfindInProgress: true,
			numWaypoints: 0);
		Assert.Equal(MoveTickKind.Wait, r.Kind);
	}

	[Fact]
	public void DecideMoveTick_waits_while_waypoints_remain()
	{
		var r = Decide(distance: 10f, numWaypoints: 4);
		Assert.Equal(MoveTickKind.Wait, r.Kind);
	}

	[Fact]
	public void DecideMoveTick_starts_mesh_path_when_ready()
	{
		var r = Decide(
			flyRequested: true,
			zoneSupportsFlying: true,
			inFlight: true,
			distance: 20f,
			playerReady: true,
			navReady: true,
			pathfindInProgress: false,
			numWaypoints: 0);
		Assert.Equal(MoveTickKind.StartMeshPath, r.Kind);
		Assert.True(r.Fly);
	}

	[Fact]
	public void DecideMoveTick_starts_direct_path_when_not_running()
	{
		var r = Decide(
			useMesh: false,
			distance: 20f,
			pathIsRunning: false);
		Assert.Equal(MoveTickKind.StartDirectPath, r.Kind);
	}

	[Fact]
	public void DecideMoveTick_waits_direct_path_when_running()
	{
		var r = Decide(
			useMesh: false,
			distance: 20f,
			pathIsRunning: true);
		Assert.Equal(MoveTickKind.Wait, r.Kind);
	}

	[Fact]
	public void Distance_matches_vector3()
	{
		var a = new Vector3(0, 0, 0);
		var b = new Vector3(3, 4, 0);
		Assert.Equal(5f, MovementDecision.Distance(a, b));
	}

	private static MoveTickResult Decide(
		bool playerValid = true,
		bool flyRequested = false,
		bool zoneSupportsFlying = true,
		bool mounted = true,
		bool inFlight = true,
		bool casting = false,
		Vector3? destination = null,
		float distance = 0.1f,
		float lastPointTolerance = 0.25f,
		bool useMesh = true,
		bool playerReady = true,
		bool navReady = true,
		bool pathfindInProgress = false,
		int numWaypoints = 0,
		bool pathIsRunning = false)
		=> MovementDecision.DecideMoveTick(
			playerValid,
			flyRequested,
			zoneSupportsFlying,
			mounted,
			inFlight,
			casting,
			destination ?? new Vector3(10, 0, 0),
			distance,
			lastPointTolerance,
			useMesh,
			playerReady,
			navReady,
			pathfindInProgress,
			numWaypoints,
			pathIsRunning);
}
