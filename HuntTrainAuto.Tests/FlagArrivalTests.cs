#nullable enable
using System.Numerics;
using Xunit;

namespace HuntTrainAuto.Tests;

public sealed class FlagArrivalTests
{
	[Fact]
	public void DefaultTolerance_is_five_yalms()
		=> Assert.Equal(5f, FlagArrival.DefaultTolerance);

	[Fact]
	public void IsArrived_null_world_false()
		=> Assert.False(FlagArrival.IsArrived(Vector3.Zero, null, 5f));

	[Fact]
	public void IsArrived_zero_world_false()
		=> Assert.False(FlagArrival.IsArrived(new Vector3(1, 0, 0), Vector3.Zero, 5f));

	[Fact]
	public void IsArrived_within_tolerance()
	{
		var player = new Vector3(0, 0, 0);
		var flag = new Vector3(4f, 0, 0);
		Assert.True(FlagArrival.IsArrived(player, flag, 5f));
		Assert.False(FlagArrival.IsArrived(player, flag, 3f));
	}

	[Fact]
	public void IsArrived_exact_tolerance_boundary()
	{
		var player = new Vector3(0, 0, 0);
		var flag = new Vector3(5f, 0, 0);
		Assert.True(FlagArrival.IsArrived(player, flag, 5f));
		Assert.False(FlagArrival.IsArrived(player, flag, 4.99f));
	}

	[Fact]
	public void IsArrived_uses_mesh_style_no_direct_path_fudge()
	{
		// MovementDecision direct-path subtracts 1 yalm; flag arrival must not.
		var player = new Vector3(0, 0, 0);
		var flag = new Vector3(1.2f, 0, 0);
		Assert.False(FlagArrival.IsArrived(player, flag, 0.25f));
		Assert.True(MovementDecision.IsArrived(flag, 1.2f, 0.25f, useMesh: false));
	}

	[Fact]
	public void ShouldStopPath_one_shot()
	{
		Assert.True(FlagArrival.ShouldStopPath(true, pathAlreadyStoppedForArrival: false));
		Assert.False(FlagArrival.ShouldStopPath(true, pathAlreadyStoppedForArrival: true));
		Assert.False(FlagArrival.ShouldStopPath(false, pathAlreadyStoppedForArrival: false));
	}

	[Fact]
	public void Evaluate_arrived_stops_path_once()
	{
		var result = FlagArrival.Evaluate(new Vector3(0, 0, 0), new Vector3(3, 0, 0), 5f);
		Assert.True(result.IsArrived);
		Assert.True(result.ShouldStopPath);
		Assert.Equal(3f, result.Distance);

		var again = FlagArrival.Evaluate(
			new Vector3(0, 0, 0),
			new Vector3(3, 0, 0),
			5f,
			pathAlreadyStoppedForArrival: true);
		Assert.True(again.IsArrived);
		Assert.False(again.ShouldStopPath);
	}

	[Fact]
	public void Evaluate_far_does_not_stop()
	{
		var result = FlagArrival.Evaluate(new Vector3(0, 0, 0), new Vector3(20, 0, 0), 5f);
		Assert.False(result.IsArrived);
		Assert.False(result.ShouldStopPath);
		Assert.Equal(20f, result.Distance);
	}

	[Fact]
	public void Evaluate_missing_world_max_distance()
	{
		var result = FlagArrival.Evaluate(Vector3.Zero, null, 5f);
		Assert.False(result.IsArrived);
		Assert.False(result.ShouldStopPath);
		Assert.Equal(float.MaxValue, result.Distance);
	}

	[Fact]
	public void Evaluate_reuses_MovementDecision_IsArrived()
	{
		var player = new Vector3(10, 2, 10);
		var flag = new Vector3(12, 2, 10);
		var distance = MovementDecision.Distance(player, flag);
		var expected = MovementDecision.IsArrived(flag, distance, FlagArrival.DefaultTolerance, useMesh: true);
		var result = FlagArrival.Evaluate(player, flag, FlagArrival.DefaultTolerance);
		Assert.Equal(expected, result.IsArrived);
		Assert.Equal(distance, result.Distance);
	}
}
