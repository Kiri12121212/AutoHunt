#nullable enable
using System.Numerics;

namespace HuntTrainAuto.Tests.Map;

public sealed class MapOpenDedupeTests
{
	[Fact]
	public void LinkPosFromRaw_divides_by_1000()
	{
		Assert.Equal(new Vector2(1.5f, -2.25f), MapOpenDedupe.LinkPosFromRaw(1500, -2250));
	}

	[Fact]
	public void ShouldOpen_when_no_existing_flag()
	{
		Assert.True(MapOpenDedupe.ShouldOpenMap(
			isFlagMarkerSet: false,
			existingTerritoryId: 0,
			existingX: 0,
			existingY: 0,
			linkTerritoryId: 123,
			linkRawX: 1000,
			linkRawY: 2000));
	}

	[Fact]
	public void ShouldOpen_when_different_territory()
	{
		Assert.True(MapOpenDedupe.ShouldOpenMap(
			isFlagMarkerSet: true,
			existingTerritoryId: 100,
			existingX: 1f,
			existingY: 2f,
			linkTerritoryId: 200,
			linkRawX: 1000,
			linkRawY: 2000));
	}

	[Fact]
	public void ShouldSkip_same_territory_within_threshold()
	{
		// Existing (1, 2); link (1000,2000) → (1, 2); distance 0 ≤ 10
		Assert.False(MapOpenDedupe.ShouldOpenMap(
			isFlagMarkerSet: true,
			existingTerritoryId: 50,
			existingX: 1f,
			existingY: 2f,
			linkTerritoryId: 50,
			linkRawX: 1000,
			linkRawY: 2000));
	}

	[Fact]
	public void ShouldSkip_at_exactly_threshold_10()
	{
		// Existing (0, 0); link (10000, 0) → (10, 0); distance == 10 → skip (≤ 10)
		Assert.False(MapOpenDedupe.ShouldOpenMap(
			isFlagMarkerSet: true,
			existingTerritoryId: 1,
			existingX: 0f,
			existingY: 0f,
			linkTerritoryId: 1,
			linkRawX: 10_000,
			linkRawY: 0));
	}

	[Fact]
	public void ShouldOpen_just_beyond_threshold()
	{
		// Existing (0, 0); link (10001, 0) → (10.001, 0); distance > 10
		Assert.True(MapOpenDedupe.ShouldOpenMap(
			isFlagMarkerSet: true,
			existingTerritoryId: 1,
			existingX: 0f,
			existingY: 0f,
			linkTerritoryId: 1,
			linkRawX: 10_001,
			linkRawY: 0));
	}

	[Theory]
	[InlineData(0, 1, 0, 0, false)]
	[InlineData(1, 0, 0, 0, false)]
	[InlineData(1, 1, 0, 0, true)]
	[InlineData(1, 1, 50_000_001, 0, false)]
	[InlineData(1, 1, 0, -50_000_001, false)]
	[InlineData(1, 1, 50_000_000, -50_000_000, true)]
	public void IsPlausibleMapLink_validates_ids_and_raw_bounds(
		uint territory,
		uint map,
		int rawX,
		int rawY,
		bool expected)
	{
		Assert.Equal(expected, MapOpenDedupe.IsPlausibleMapLink(territory, map, rawX, rawY));
	}

	[Fact]
	public void DuplicateDistanceThreshold_is_10()
	{
		Assert.Equal(10f, MapOpenDedupe.DuplicateDistanceThreshold);
	}

	[Fact]
	public void Describe_covers_open_and_dedupe_skip()
	{
		Assert.Equal(
			"open: no existing flag",
			MapOpenDedupe.Describe(true, false, 0, 1, 0f));
		Assert.Equal(
			"open: territory 1 → 2",
			MapOpenDedupe.Describe(true, true, 1, 2, 0f));
		Assert.Equal(
			"skip duplicate: distance 10.00 <= 10.00",
			MapOpenDedupe.Describe(false, true, 1, 1, 10f));
		Assert.Equal(
			"open: distance 10.01 > 10.00",
			MapOpenDedupe.Describe(true, true, 1, 1, 10.01f));
	}
}
