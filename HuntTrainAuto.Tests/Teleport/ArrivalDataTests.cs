#nullable enable
using System;

namespace HuntTrainAuto.Tests.Teleport;

public sealed class ArrivalDataTests
{
	[Fact]
	public void CreateOrNull_from_ids_returns_plan()
	{
		var arrival = ArrivalData.CreateOrNull(42u, 813u, 2, "  Spriggan  ");

		Assert.NotNull(arrival);
		Assert.Equal(42u, arrival.AetheryteId);
		Assert.Equal(813u, arrival.Territory);
		Assert.Equal(2, arrival.Instance);
		Assert.Equal("Spriggan", arrival.World);
	}

	[Fact]
	public void CreateOrNull_from_ids_allows_null_world()
	{
		var arrival = ArrivalData.CreateOrNull(1u, 100u, 0);
		Assert.NotNull(arrival);
		Assert.Null(arrival.World);
		Assert.Equal(0, arrival.Instance);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void CreateOrNull_nulls_blank_world(string? world)
	{
		var arrival = ArrivalData.CreateOrNull(1u, 100u, 1, world);
		Assert.NotNull(arrival);
		Assert.Null(arrival.World);
	}

	[Fact]
	public void CreateOrNull_returns_null_when_aetheryte_id_zero()
	{
		Assert.Null(ArrivalData.CreateOrNull(0u, 813u, 1));
	}

	[Fact]
	public void CreateOrNull_from_nearest_maps_row_id()
	{
		var nearest = new NearestAetheryteResult(99u, "Fort Jobb");
		var arrival = ArrivalData.CreateOrNull(nearest, 813u, 3, "Cerberus");

		Assert.NotNull(arrival);
		Assert.Equal(99u, arrival.AetheryteId);
		Assert.Equal(813u, arrival.Territory);
		Assert.Equal(3, arrival.Instance);
		Assert.Equal("Cerberus", arrival.World);
	}

	[Fact]
	public void CreateOrNull_from_nearest_returns_null_when_missing()
	{
		Assert.Null(ArrivalData.CreateOrNull((NearestAetheryteResult?)null, 813u, 1));
	}

	[Fact]
	public void CreateOrNull_from_nearest_returns_null_when_row_id_zero()
	{
		var nearest = new NearestAetheryteResult(0u, "Invalid");
		Assert.Null(ArrivalData.CreateOrNull(nearest, 813u, 1));
	}

	[Fact]
	public void Attach_sets_flag_arrival_from_nearest()
	{
		var flag = HuntFlag.FromMapLink(813u, 456u, 1000, 2000, "Lakeland", DateTimeOffset.UnixEpoch);
		var nearest = new NearestAetheryteResult(42u, "Fort Jobb");

		var arrival = ArrivalData.Attach(flag, nearest, 2);

		Assert.NotNull(arrival);
		Assert.Same(arrival, flag.Arrival);
		Assert.Equal(42u, flag.Arrival!.AetheryteId);
		Assert.Equal(813u, flag.Arrival.Territory);
		Assert.Equal(2, flag.Arrival.Instance);
	}

	[Fact]
	public void Attach_clears_arrival_when_nearest_null()
	{
		var flag = HuntFlag.FromMapLink(813u, 456u, 1000, 2000, "Lakeland", DateTimeOffset.UnixEpoch);
		flag.Arrival = ArrivalData.CreateOrNull(1u, 813u, 1);

		var result = ArrivalData.Attach(flag, null, 1);

		Assert.Null(result);
		Assert.Null(flag.Arrival);
	}

	[Fact]
	public void Attach_throws_when_flag_null()
	{
		Assert.Throws<ArgumentNullException>(() => ArrivalData.Attach(null!, new NearestAetheryteResult(1u, "A"), 0));
	}
}
