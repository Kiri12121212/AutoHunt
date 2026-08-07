#nullable enable
using System;

namespace HuntTrainAuto.Tests.Map;

public sealed class MapCoordinatesTests
{
	[Fact]
	public void ConvertRawPosition_at_origin_marker_raw_zero_scale_100()
	{
		// pos=0, scale=100 → num=1 → (0 + 1024)/2048 * 41 + 1 = 21.5
		Assert.Equal(21.5f, MapCoordinates.ConvertRawPositionToMapCoordinate(0, 100f));
	}

	[Fact]
	public void ConvertMapMarker_at_1024_scale_100_is_21_5()
	{
		// Marker center 1024 → raw 0 → 21.5
		Assert.Equal(21.5f, MapCoordinates.ConvertMapMarkerToMapCoordinate(1024, 100f));
	}

	[Fact]
	public void ConvertMapMarker_matches_raw_pipeline()
	{
		const int markerPos = 1500;
		const float scale = 100f;
		var num = scale / 100f;
		var raw = (int)((markerPos - 1024.0) / num * 1000f);
		var expected = MapCoordinates.ConvertRawPositionToMapCoordinate(raw, scale);
		Assert.Equal(expected, MapCoordinates.ConvertMapMarkerToMapCoordinate(markerPos, scale));
	}

	[Theory]
	[InlineData(100)]
	[InlineData(95)]
	[InlineData(200)]
	public void ConvertRaw_formula_parity(float scale)
	{
		const int pos = 12_345;
		var num = scale / 100f;
		var expected = (float)((pos / 1000f * num + 1024.0) / 2048.0 * 41.0 / num + 1.0);
		Assert.Equal(expected, MapCoordinates.ConvertRawPositionToMapCoordinate(pos, scale));
	}

	[Fact]
	public void ConvertMapMarker_scale_200()
	{
		const int pos = 1024;
		const float scale = 200f;
		var num = scale / 100f;
		var raw = (int)((pos - 1024.0) / num * 1000f);
		var expected = (float)((raw / 1000f * num + 1024.0) / 2048.0 * 41.0 / num + 1.0);
		Assert.Equal(expected, MapCoordinates.ConvertMapMarkerToMapCoordinate(pos, scale));
	}

	[Theory]
	[InlineData(0f, 100f, 0)]
	[InlineData(0f, 100f, 100)]
	[InlineData(50f, 100f, -50)]
	[InlineData(12.5f, 95f, 42)]
	public void ConvertWorld_matches_saintcoinach_3d(float world, float scale, int offset)
	{
		var num = scale / 100f;
		var expected = (float)(((world + offset) * num + 1024.0) / 2048.0 * 41.0 / num + 1.0);
		Assert.Equal(expected, MapCoordinates.ConvertWorldToMapCoordinate(world, scale, offset));
	}

	[Fact]
	public void ConvertWorld_with_offset_differs_from_zero_offset()
	{
		const float world = 100f;
		const float scale = 100f;
		var without = MapCoordinates.ConvertWorldToMapCoordinate(world, scale, offset: 0);
		var with = MapCoordinates.ConvertWorldToMapCoordinate(world, scale, offset: 200);
		Assert.NotEqual(without, with);
	}

	[Theory]
	[InlineData(0f, 100f, 0)]
	[InlineData(50f, 100f, -50)]
	[InlineData(12.5f, 95f, 42)]
	[InlineData(-20f, 200f, 100)]
	public void ConvertMapToWorld_roundtrips_ConvertWorld(float world, float scale, int offset)
	{
		var map = MapCoordinates.ConvertWorldToMapCoordinate(world, scale, offset);
		var back = MapCoordinates.ConvertMapCoordinateToWorld(map, scale, offset);
		Assert.Equal(world, back, precision: 3);
	}

	[Fact]
	public void MapDistance_same_point_is_zero()
	{
		Assert.Equal(0f, MapCoordinates.MapDistance(12.3f, 4.5f, 12.3f, 4.5f));
	}
}
