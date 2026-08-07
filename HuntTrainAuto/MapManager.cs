#nullable enable
using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace HuntTrainAuto;

/// <summary>
/// Game-bound nearest-aetheryte scan (HTA <c>MapManager.GetNearestAetheryte</c>).
/// Does not teleport or fill <see cref="ArrivalData"/>.
/// </summary>
public sealed class MapManager
{
	private readonly IDataManager dataManager;
	private readonly Action<string>? logError;

	public MapManager(IDataManager dataManager, Action<string>? logError = null)
	{
		this.dataManager = dataManager ?? throw new ArgumentNullException(nameof(dataManager));
		this.logError = logError;
	}

	/// <summary>
	/// Nearest aetheryte on <paramref name="territoryTypeId"/> to map-link coords
	/// (same units as <c>MapLinkPayload.XCoord</c> / <c>YCoord</c>).
	/// Uses <paramref name="mapId"/> for <c>SizeFactor</c> when present; else first map for the territory.
	/// </summary>
	public NearestAetheryteResult? GetNearestAetheryte(
		uint territoryTypeId,
		uint mapId,
		float xCoord,
		float yCoord,
		IReadOnlyList<uint> aetheryteBlacklist,
		bool distanceCompensationHack)
	{
		ArgumentNullException.ThrowIfNull(aetheryteBlacklist);

		var aetheryteSheet = dataManager.GetExcelSheet<Aetheryte>();
		var mapSheet = dataManager.GetExcelSheet<Map>();
		var markerSheet = dataManager.GetSubrowExcelSheet<MapMarker>();
		if (aetheryteSheet == null || mapSheet == null || markerSheet == null)
			return null;

		var scale = MapSizeFactor.Resolve(mapId, territoryTypeId, EnumerateMapScales(mapSheet));
		if (scale == null)
			return null;

		var sizeFactor = scale.Value;
		var candidates = new List<NearestAetheryte.Candidate>();

		foreach (var aetheryte in aetheryteSheet)
		{
			if (!aetheryte.IsAetheryte)
				continue;
			if (aetheryte.Territory.ValueNullable == null)
				continue;
			if (aetheryte.PlaceName.ValueNullable == null)
				continue;
			if (aetheryte.Territory.RowId != territoryTypeId)
				continue;

			var placeName = aetheryte.PlaceName.Value.Name.ToString();
			if (string.IsNullOrEmpty(placeName))
				placeName = $"#{aetheryte.RowId}";

			MapMarker? marker = null;
			foreach (var m in markerSheet.Flatten())
			{
				if (m.DataType == 3 && m.DataKey.RowId == aetheryte.RowId)
				{
					marker = m;
					break;
				}
			}

			if (marker == null)
			{
				logError?.Invoke($"Cannot find aetheryte position for #{aetheryte.RowId} ({placeName})");
				continue;
			}

			var delta = DistanceCompensation.GetDelta(placeName, distanceCompensationHack);
			var mapX = MapCoordinates.ConvertMapMarkerToMapCoordinate(marker.Value.X, sizeFactor) + delta.X;
			var mapY = MapCoordinates.ConvertMapMarkerToMapCoordinate(marker.Value.Y, sizeFactor) + delta.Y;
			candidates.Add(new NearestAetheryte.Candidate(aetheryte.RowId, placeName, mapX, mapY));
		}

		IReadOnlyCollection<uint> blacklist = aetheryteBlacklist is IReadOnlyCollection<uint> collection
			? collection
			: new HashSet<uint>(aetheryteBlacklist);
		return NearestAetheryte.Select(xCoord, yCoord, candidates, blacklist);
	}

	private static IEnumerable<(uint RowId, uint TerritoryTypeId, float SizeFactor)> EnumerateMapScales(
		Lumina.Excel.ExcelSheet<Map> mapSheet)
	{
		foreach (var map in mapSheet)
			yield return (map.RowId, map.TerritoryType.RowId, map.SizeFactor);
	}
}
