#nullable enable
using System;
using System.Collections.Generic;

namespace HuntTrainAuto.Map;

/// <summary>
/// Map sheet params for marker/world → map conversion.
/// Prefers a specific map row when the caller has a <c>MapId</c>.
/// <see cref="MapId"/> is the resolved Lumina <c>Map</c> row id (0 only when unresolved).
/// </summary>
public readonly record struct MapCoordParams(uint MapId, float SizeFactor, int OffsetX, int OffsetY);

/// <summary>
/// Picks <c>Map.SizeFactor</c> (and offsets) for marker→map conversion.
/// Prefers a specific map row when the caller has a <c>MapId</c>.
/// </summary>
public static class MapSizeFactor
{
	/// <summary>
	/// Prefer <paramref name="mapId"/> when non-zero and present; else first map for
	/// <paramref name="territoryTypeId"/> (HTA territory scan parity).
	/// </summary>
	public static float? Resolve(
		uint mapId,
		uint territoryTypeId,
		IEnumerable<(uint RowId, uint TerritoryTypeId, float SizeFactor)> maps)
	{
		ArgumentNullException.ThrowIfNull(maps);

		float? byTerritory = null;
		foreach (var map in maps)
		{
			if (mapId != 0 && map.RowId == mapId)
				return map.SizeFactor;

			if (byTerritory == null && map.TerritoryTypeId == territoryTypeId)
				byTerritory = map.SizeFactor;
		}

		return byTerritory;
	}

	/// <summary>
	/// Same map-row preference as <see cref="Resolve"/>, including Lumina <c>OffsetX</c>/<c>OffsetY</c>.
	/// </summary>
	public static MapCoordParams? ResolveParams(
		uint mapId,
		uint territoryTypeId,
		IEnumerable<(uint RowId, uint TerritoryTypeId, float SizeFactor, int OffsetX, int OffsetY)> maps)
	{
		ArgumentNullException.ThrowIfNull(maps);

		MapCoordParams? byTerritory = null;
		foreach (var map in maps)
		{
			var p = new MapCoordParams(map.RowId, map.SizeFactor, map.OffsetX, map.OffsetY);
			if (mapId != 0 && map.RowId == mapId)
				return p;

			if (byTerritory == null && map.TerritoryTypeId == territoryTypeId)
				byTerritory = p;
		}

		return byTerritory;
	}
}
