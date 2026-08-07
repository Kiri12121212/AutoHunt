#nullable enable
using System;
using System.Numerics;
using Xunit;

namespace HuntTrainAuto.Tests;

public sealed class HuntFlagTests
{
	[Fact]
	public void FromMapLink_captures_territory_coords_and_place()
	{
		var ts = new DateTimeOffset(2026, 8, 7, 12, 0, 0, TimeSpan.Zero);
		var flag = HuntFlag.FromMapLink(123, 456, 1000, 2000, "  Lakeland  ", ts);

		Assert.Equal(123u, flag.TerritoryTypeId);
		Assert.Equal(456u, flag.MapId);
		Assert.Equal(1000, flag.RawX);
		Assert.Equal(2000, flag.RawY);
		Assert.Equal("Lakeland", flag.PlaceName);
		Assert.Equal(ts, flag.Timestamp);
		Assert.Null(flag.WorldPos);
		Assert.Null(flag.Arrival);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void FromMapLink_nulls_blank_place_name(string? placeName)
	{
		var flag = HuntFlag.FromMapLink(1, 2, 3, 4, placeName, DateTimeOffset.UnixEpoch);
		Assert.Null(flag.PlaceName);
	}

	[Fact]
	public void FromMapLink_defaults_timestamp_to_utc_now()
	{
		var before = DateTimeOffset.UtcNow.AddSeconds(-2);
		var flag = HuntFlag.FromMapLink(1, 2, 3, 4, "Zone");
		var after = DateTimeOffset.UtcNow.AddSeconds(2);

		Assert.InRange(flag.Timestamp, before, after);
	}

	[Fact]
	public void WorldPos_and_Arrival_stubs_are_settable()
	{
		var flag = HuntFlag.FromMapLink(1, 2, 3, 4, "Zone", DateTimeOffset.UnixEpoch);
		flag.WorldPos = new Vector3(1, 2, 3);
		flag.Arrival = new ArrivalData();

		Assert.Equal(new Vector3(1, 2, 3), flag.WorldPos);
		Assert.NotNull(flag.Arrival);
	}
}
