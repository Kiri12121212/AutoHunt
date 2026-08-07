#nullable enable
using System;
using Xunit;

namespace HuntTrainAuto.Tests;

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
}
