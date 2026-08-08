#nullable enable
using System;

namespace HuntTrainAuto.Map;

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

	/// <summary>
	/// World X/Z → map coordinate (Dalamud/SaintCoinach <c>ToMapCoordinate3d</c>).
	/// <paramref name="offset"/> is Lumina <c>Map.OffsetX</c> / <c>OffsetY</c>.
	/// </summary>
	public static float ConvertWorldToMapCoordinate(float world, float scale, int offset = 0)
	{
		var num = scale / 100f;
		return (float)(((world + offset) * num + 1024.0) / 2048.0 * 41.0 / num + 1.0);
	}

	/// <summary>
	/// Map coordinate → world X/Z (inverse of <see cref="ConvertWorldToMapCoordinate"/>).
	/// Pure; no Dalamud. Flag navigation still projects via PointOnFloor.
	/// </summary>
	public static float ConvertMapCoordinateToWorld(float mapCoord, float scale, int offset = 0)
	{
		var num = scale / 100f;
		return (float)((((mapCoord - 1.0) * num / 41.0 * 2048.0) - 1024.0) / num) - offset;
	}

	/// <summary>
	/// Human-readable map coordinate → raw position (Dalamud <c>MapLinkPayload</c> inverse).
	/// <paramref name="offset"/> is Lumina <c>Map.OffsetX</c> / <c>OffsetY</c>.
	/// </summary>
	public static int ConvertMapCoordinateToRawPosition(float mapCoord, float scale, int offset = 0)
	{
		var trueScale = scale / 100f;
		var num2 = (float)((((mapCoord - 1.0) * trueScale / 41.0 * 2048.0) - 1024.0) / trueScale);
		num2 *= 1000f;
		return (int)num2 - (offset * 1000);
	}

	/// <summary>Euclidean map distance between two map-coordinate points.</summary>
	public static float MapDistance(float x1, float y1, float x2, float y2)
	{
		var dx = x1 - x2;
		var dy = y1 - y2;
		return MathF.Sqrt((dx * dx) + (dy * dy));
	}

	/// <summary>Euclidean world XZ distance in yalms (ignores Y).</summary>
	public static float WorldXZDistance(float x1, float z1, float x2, float z2)
	{
		var dx = x1 - x2;
		var dz = z1 - z2;
		return MathF.Sqrt((dx * dx) + (dz * dz));
	}
}
