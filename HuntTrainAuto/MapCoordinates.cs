#nullable enable
using System;

namespace HuntTrainAuto;

/// <summary>
/// HTA <c>MapManager</c> map-marker / raw-position → map-coordinate conversion (pure).
/// </summary>
public static class MapCoordinates
{
	/// <summary>HTA <c>ConvertMapMarkerToMapCoordinate</c>.</summary>
	public static float ConvertMapMarkerToMapCoordinate(int pos, float scale)
	{
		var num = scale / 100f;
		var rawPosition = (int)((pos - 1024.0) / num * 1000f);
		return ConvertRawPositionToMapCoordinate(rawPosition, scale);
	}

	/// <summary>HTA <c>ConvertRawPositionToMapCoordinate</c>.</summary>
	public static float ConvertRawPositionToMapCoordinate(int pos, float scale)
	{
		var num = scale / 100f;
		return (float)((pos / 1000f * num + 1024.0) / 2048.0 * 41.0 / num + 1.0);
	}
}
