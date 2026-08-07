#nullable enable

using System;
using System.Collections.Generic;
using HuntTrainAuto.Domain;
using HuntTrainAuto.HuntAlerts;
using HuntTrainAuto.Map;

namespace HuntTrainAuto.Tests.HuntAlerts;

public sealed class HuntTrainMessageMapperTests
{
	private static HuntTrainMessage ValidMessage(
		string huntType = HuntAlertsFilter.HuntTypeATrain,
		string world = "Phoenix",
		uint territory = 813,
		uint aetheryteId = 42,
		string startLocation = "Fort Jobb",
		float mapX = 12.3f,
		float mapY = 24.5f,
		int instance = 2,
		string locationCoords = "")
		=> new()
		{
			huntType = huntType,
			huntWorld = world,
			startTerritoryTypeId = territory,
			startLocationAetheryteId = aetheryteId,
			startLocation = startLocation,
			startZone = "Lakeland",
			mapLocationX = mapX,
			mapLocationY = mapY,
			locationCoords = locationCoords,
			instance = instance,
		};

	[Fact]
	public void TryMap_builds_flag_and_arrival_from_mapLocation()
	{
		var message = ValidMessage();
		var ts = DateTimeOffset.UnixEpoch;

		Assert.True(HuntTrainMessageMapper.TryMap(
			message,
			huntAlertsIntegration: true,
			rankFilter: null,
			worldBlacklist: null,
			out var flag,
			mapId: 456,
			sizeFactor: 100f,
			timestamp: ts));

		Assert.Equal(813u, flag.TerritoryTypeId);
		Assert.Equal(456u, flag.MapId);
		Assert.Equal("Fort Jobb", flag.PlaceName);
		Assert.Equal(ts, flag.Timestamp);

		var expectedX = MapCoordinates.ConvertMapCoordinateToRawPosition(
			12.3f + HuntTrainMessageMapper.DefaultMapCoordFudge, 100f);
		var expectedY = MapCoordinates.ConvertMapCoordinateToRawPosition(
			24.5f + HuntTrainMessageMapper.DefaultMapCoordFudge, 100f);
		Assert.Equal(expectedX, flag.RawX);
		Assert.Equal(expectedY, flag.RawY);

		Assert.NotNull(flag.Arrival);
		Assert.Equal(42u, flag.Arrival!.AetheryteId);
		Assert.Equal(813u, flag.Arrival.Territory);
		Assert.Equal(2, flag.Arrival.Instance);
		Assert.Equal("Phoenix", flag.Arrival.World);
		Assert.Equal("Phoenix", flag.HuntWorld);
	}

	[Fact]
	public void TryMap_uses_locationCoords_when_mapLocation_unset()
	{
		var message = ValidMessage(mapX: 0f, mapY: 0f, locationCoords: "15.5, 8.25");

		Assert.True(HuntTrainMessageMapper.TryMap(
			message,
			huntAlertsIntegration: true,
			rankFilter: null,
			worldBlacklist: null,
			out var flag,
			sizeFactor: 100f,
			timestamp: DateTimeOffset.UnixEpoch));

		var expectedX = MapCoordinates.ConvertMapCoordinateToRawPosition(
			15.5f + HuntTrainMessageMapper.DefaultMapCoordFudge, 100f);
		var expectedY = MapCoordinates.ConvertMapCoordinateToRawPosition(
			8.25f + HuntTrainMessageMapper.DefaultMapCoordFudge, 100f);
		Assert.Equal(expectedX, flag.RawX);
		Assert.Equal(expectedY, flag.RawY);
	}

	[Fact]
	public void TryMap_prefers_mapLocation_over_locationCoords()
	{
		var message = ValidMessage(mapX: 10f, mapY: 20f, locationCoords: "1.0, 2.0");

		Assert.True(HuntTrainMessageMapper.TryMap(
			message,
			huntAlertsIntegration: true,
			rankFilter: null,
			worldBlacklist: null,
			out var flag,
			sizeFactor: 100f,
			timestamp: DateTimeOffset.UnixEpoch));

		var expectedX = MapCoordinates.ConvertMapCoordinateToRawPosition(
			10f + HuntTrainMessageMapper.DefaultMapCoordFudge, 100f);
		Assert.Equal(expectedX, flag.RawX);
	}

	[Fact]
	public void TryMap_omits_arrival_when_aetheryte_id_zero()
	{
		var message = ValidMessage(aetheryteId: 0);

		Assert.True(HuntTrainMessageMapper.TryMap(
			message,
			huntAlertsIntegration: true,
			rankFilter: null,
			worldBlacklist: null,
			out var flag,
			timestamp: DateTimeOffset.UnixEpoch));

		Assert.Null(flag.Arrival);
		Assert.Equal("Phoenix", flag.HuntWorld);
	}

	[Fact]
	public void TryMap_falls_back_to_startZone_for_place_name()
	{
		var message = ValidMessage(startLocation: "  ");
		message.startZone = "Lakeland";

		Assert.True(HuntTrainMessageMapper.TryMap(
			message,
			huntAlertsIntegration: true,
			rankFilter: null,
			worldBlacklist: null,
			out var flag,
			timestamp: DateTimeOffset.UnixEpoch));

		Assert.Equal("Lakeland", flag.PlaceName);
	}

	[Fact]
	public void TryMap_applies_size_factor_and_offsets()
	{
		var message = ValidMessage(mapX: 21.5f, mapY: 21.5f);

		Assert.True(HuntTrainMessageMapper.TryMap(
			message,
			huntAlertsIntegration: true,
			rankFilter: null,
			worldBlacklist: null,
			out var flag,
			mapId: 77,
			sizeFactor: 200f,
			offsetX: 10,
			offsetY: -5,
			mapCoordFudge: 0f,
			timestamp: DateTimeOffset.UnixEpoch));

		Assert.Equal(77u, flag.MapId);
		Assert.Equal(
			MapCoordinates.ConvertMapCoordinateToRawPosition(21.5f, 200f, 10),
			flag.RawX);
		Assert.Equal(
			MapCoordinates.ConvertMapCoordinateToRawPosition(21.5f, 200f, -5),
			flag.RawY);
	}

	[Fact]
	public void UnpackMapParams_uses_resolved_sheet_row()
	{
		HuntTrainMessageMapper.UnpackMapParams(
			new MapCoordParams(MapId: 42, SizeFactor: 200f, OffsetX: 3, OffsetY: -4),
			out var mapId,
			out var sizeFactor,
			out var offsetX,
			out var offsetY);

		Assert.Equal(42u, mapId);
		Assert.Equal(200f, sizeFactor);
		Assert.Equal(3, offsetX);
		Assert.Equal(-4, offsetY);
	}

	[Fact]
	public void UnpackMapParams_defaults_when_resolve_null()
	{
		HuntTrainMessageMapper.UnpackMapParams(
			null,
			out var mapId,
			out var sizeFactor,
			out var offsetX,
			out var offsetY);

		Assert.Equal(0u, mapId);
		Assert.Equal(HuntTrainMessageMapper.DefaultSizeFactor, sizeFactor);
		Assert.Equal(0, offsetX);
		Assert.Equal(0, offsetY);
	}

	[Fact]
	public void UnpackMapParams_replaces_non_positive_size_factor()
	{
		HuntTrainMessageMapper.UnpackMapParams(
			new MapCoordParams(MapId: 9, SizeFactor: 0f, OffsetX: 1, OffsetY: 2),
			out _,
			out var sizeFactor,
			out _,
			out _);

		Assert.Equal(HuntTrainMessageMapper.DefaultSizeFactor, sizeFactor);
	}

	[Fact]
	public void TryMap_uses_unpacked_resolver_params()
	{
		var message = ValidMessage(mapX: 21.5f, mapY: 21.5f);
		var resolved = new MapCoordParams(55, 200f, 10, -5);
		HuntTrainMessageMapper.UnpackMapParams(
			resolved,
			out var mapId,
			out var sizeFactor,
			out var offsetX,
			out var offsetY);

		Assert.True(HuntTrainMessageMapper.TryMap(
			message,
			huntAlertsIntegration: true,
			rankFilter: null,
			worldBlacklist: null,
			out var flag,
			mapId,
			sizeFactor,
			offsetX,
			offsetY,
			mapCoordFudge: 0f,
			timestamp: DateTimeOffset.UnixEpoch));

		Assert.Equal(55u, flag.MapId);
		Assert.Equal(
			MapCoordinates.ConvertMapCoordinateToRawPosition(21.5f, 200f, 10),
			flag.RawX);
		Assert.Equal(
			MapCoordinates.ConvertMapCoordinateToRawPosition(21.5f, 200f, -5),
			flag.RawY);
	}

	[Fact]
	public void TryMap_rejects_when_integration_off()
	{
		Assert.False(HuntTrainMessageMapper.TryMap(
			ValidMessage(),
			huntAlertsIntegration: false,
			rankFilter: null,
			worldBlacklist: null,
			out _));
	}

	[Fact]
	public void TryMap_rejects_unknown_hunt_type()
	{
		Assert.False(HuntTrainMessageMapper.TryMap(
			ValidMessage(huntType: "b_rank"),
			huntAlertsIntegration: true,
			rankFilter: null,
			worldBlacklist: null,
			out _));
	}

	[Fact]
	public void TryMap_rejects_rank_not_in_filter()
	{
		Assert.False(HuntTrainMessageMapper.TryMap(
			ValidMessage(huntType: HuntAlertsFilter.HuntTypeSRank),
			huntAlertsIntegration: true,
			rankFilter: [HuntMarkRank.A],
			worldBlacklist: null,
			out _));
	}

	[Fact]
	public void TryMap_rejects_blacklisted_world()
	{
		Assert.False(HuntTrainMessageMapper.TryMap(
			ValidMessage(world: "Phoenix"),
			huntAlertsIntegration: true,
			rankFilter: null,
			worldBlacklist: ["phoenix"],
			out _));
	}

	[Fact]
	public void TryMap_rejects_missing_territory()
	{
		var message = ValidMessage();
		message.startTerritoryTypeId = 0;
		Assert.False(HuntTrainMessageMapper.TryMap(
			message,
			huntAlertsIntegration: true,
			rankFilter: null,
			worldBlacklist: null,
			out _));
	}

	[Fact]
	public void TryMap_rejects_missing_coords()
	{
		var message = ValidMessage(mapX: 0f, mapY: 0f, locationCoords: "");
		Assert.False(HuntTrainMessageMapper.TryMap(
			message,
			huntAlertsIntegration: true,
			rankFilter: null,
			worldBlacklist: null,
			out _));
	}

	[Fact]
	public void TryMap_rejects_null_message()
	{
		Assert.False(HuntTrainMessageMapper.TryMap(
			null,
			huntAlertsIntegration: true,
			rankFilter: null,
			worldBlacklist: null,
			out _));
	}

	[Theory]
	[InlineData("12.3, 24.5", 12.3f, 24.5f)]
	[InlineData("12.3,24.5", 12.3f, 24.5f)]
	[InlineData(" 1.0 , 2.0 ", 1.0f, 2.0f)]
	public void TryParseLocationCoords_accepts_variants(string raw, float x, float y)
	{
		Assert.True(HuntTrainMessageMapper.TryParseLocationCoords(raw, out var mx, out var my));
		Assert.Equal(x, mx);
		Assert.Equal(y, my);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("12.3")]
	[InlineData("a, b")]
	public void TryParseLocationCoords_rejects_invalid(string? raw)
		=> Assert.False(HuntTrainMessageMapper.TryParseLocationCoords(raw, out _, out _));
}
