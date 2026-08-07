#nullable enable
using System;
using System.Collections.Generic;

namespace HuntTrainAuto;

/// <summary>
/// Picks <c>Map.SizeFactor</c> for marker→map conversion.
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
}
