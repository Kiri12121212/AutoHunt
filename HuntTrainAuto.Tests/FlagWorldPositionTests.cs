#nullable enable
using System;
using System.Numerics;
using Xunit;

namespace HuntTrainAuto.Tests;

public sealed class FlagWorldPositionTests
{
	[Fact]
	public void WorldXZFromRaw_divides_milli_units()
	{
		var xz = FlagWorldPosition.WorldXZFromRaw(12_345, -6_789);
		Assert.Equal(12.345f, xz.X);
		Assert.Equal(-6.789f, xz.Y);
	}

	[Fact]
	public void ApproximateFromRaw_uses_y_and_xz()
	{
		var approx = FlagWorldPosition.ApproximateFromRaw(1000, 2000, y: 0f);
		Assert.Equal(new Vector3(1f, 0f, 2f), approx);
	}

	[Fact]
	public void PointOnFloorQueryFromRaw_uses_ad_query_height()
	{
		var query = FlagWorldPosition.PointOnFloorQueryFromRaw(5000, 7000);
		Assert.Equal(5f, query.X);
		Assert.Equal(FlagWorldPosition.PointOnFloorQueryY, query.Y);
		Assert.Equal(7f, query.Z);
	}

	[Fact]
	public void PointOnFloorQueryFromWorldXZ_uses_ad_query_height()
	{
		var query = FlagWorldPosition.PointOnFloorQueryFromWorldXZ(10.5f, -3.25f);
		Assert.Equal(new Vector3(10.5f, FlagWorldPosition.PointOnFloorQueryY, -3.25f), query);
	}

	[Fact]
	public void ChooseWorldPos_returns_floor_hit()
	{
		var floor = new Vector3(1f, 42f, 3f);
		Assert.Equal(floor, FlagWorldPosition.ChooseWorldPos(floor));
	}

	[Fact]
	public void ChooseWorldPos_null_floor_returns_null_not_approximate()
	{
		// Soft-fail: missing mesh / no hit → do not navigate on Y=0 approximate.
		Assert.Null(FlagWorldPosition.ChooseWorldPos(null));
	}

	[Fact]
	public void Attach_sets_WorldPos_from_floor()
	{
		var flag = HuntFlag.FromMapLink(813u, 1u, 1000, 2000, "A", DateTimeOffset.UnixEpoch);
		var floor = new Vector3(1f, 50f, 2f);

		var result = FlagWorldPosition.Attach(flag, floor);

		Assert.Equal(floor, result);
		Assert.Equal(floor, flag.WorldPos);
	}

	[Fact]
	public void Attach_clears_WorldPos_when_floor_null()
	{
		var flag = HuntFlag.FromMapLink(813u, 1u, 1000, 2000, "A", DateTimeOffset.UnixEpoch);
		flag.WorldPos = new Vector3(9f, 9f, 9f);

		var result = FlagWorldPosition.Attach(flag, null);

		Assert.Null(result);
		Assert.Null(flag.WorldPos);
	}

	[Fact]
	public void Attach_throws_on_null_flag()
		=> Assert.Throws<ArgumentNullException>(() => FlagWorldPosition.Attach(null!, new Vector3(1, 2, 3)));

	[Fact]
	public void Ad_defaults_match_maphelper()
	{
		Assert.Equal(1024f, FlagWorldPosition.PointOnFloorQueryY);
		Assert.Equal(5f, FlagWorldPosition.DefaultHalfExtentXZ);
		Assert.False(FlagWorldPosition.DefaultAllowUnlandable);
	}
}
