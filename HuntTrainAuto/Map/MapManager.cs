#nullable enable
using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using LuminaMap = Lumina.Excel.Sheets.Map;

namespace HuntTrainAuto.Map;

/// <summary>
/// Game-bound nearest-aetheryte scan (HTA <c>MapManager.GetNearestAetheryte</c>).
/// Does not teleport or fill <see cref="ArrivalData"/>.
/// </summary>
public sealed class MapManager
{
	private readonly IDataManager dataManager;
	private readonly Action<string>? logError;
	private readonly Action<string>? logDebug;

	public MapManager(
		IDataManager dataManager,
		Action<string>? logError = null,
		Action<string>? logDebug = null)
	{
		this.dataManager = dataManager ?? throw new ArgumentNullException(nameof(dataManager));
		this.logError = logError;
		this.logDebug = logDebug;
	}

	/// <summary>
	/// <c>Map.SizeFactor</c> for <paramref name="mapId"/> / territory (null when sheet/map missing).
	/// </summary>
	public float? GetSizeFactor(uint mapId, uint territoryTypeId)
		=> GetMapParams(mapId, territoryTypeId)?.SizeFactor;

	/// <summary>
	/// Size factor + <c>OffsetX</c>/<c>OffsetY</c> for world↔map conversion
	/// (null when sheet/map missing).
	/// </summary>
	public MapCoordParams? GetMapParams(uint mapId, uint territoryTypeId)
	{
		var mapSheet = dataManager.GetExcelSheet<LuminaMap>();
		if (mapSheet == null)
		{
			logDebug?.Invoke("[Map] map sheet unavailable");
			return null;
		}

		var result = MapSizeFactor.ResolveParams(mapId, territoryTypeId, EnumerateMapParams(mapSheet));
		if (result == null)
			logDebug?.Invoke($"[Map] map params unavailable: map={mapId}, territory={territoryTypeId}");
		return result;
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
		IReadOnlyList<uint> aetheryteBlacklist)
	{
		ArgumentNullException.ThrowIfNull(aetheryteBlacklist);

		var aetheryteSheet = dataManager.GetExcelSheet<Aetheryte>();
		var mapSheet = dataManager.GetExcelSheet<LuminaMap>();
		var markerSheet = dataManager.GetSubrowExcelSheet<MapMarker>();
		if (aetheryteSheet == null || mapSheet == null || markerSheet == null)
		{
			logDebug?.Invoke(
				$"[Map] aetheryte data unavailable: aetherytes={aetheryteSheet != null}, maps={mapSheet != null}, markers={markerSheet != null}");
			return null;
		}

		var mapParams = MapSizeFactor.ResolveParams(mapId, territoryTypeId, EnumerateMapParams(mapSheet));
		if (mapParams == null)
		{
			logDebug?.Invoke($"[Map] aetheryte search skipped: map params unavailable for map={mapId}, territory={territoryTypeId}");
			return null;
		}

		var sizeFactor = mapParams.Value.SizeFactor;
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
				logError?.Invoke($"[Map] cannot find aetheryte position for #{aetheryte.RowId} ({placeName})");
				logDebug?.Invoke($"[Map] skipped #{aetheryte.RowId} ({placeName}): marker unavailable");
				continue;
			}

			var mapX = MapCoordinates.ConvertMapMarkerToMapCoordinate(marker.Value.X, sizeFactor);
			var mapY = MapCoordinates.ConvertMapMarkerToMapCoordinate(marker.Value.Y, sizeFactor);
			candidates.Add(new NearestAetheryte.Candidate(aetheryte.RowId, placeName, mapX, mapY));
		}

		IReadOnlyCollection<uint> blacklist = aetheryteBlacklist is IReadOnlyCollection<uint> collection
			? collection
			: new HashSet<uint>(aetheryteBlacklist);
		var result = NearestAetheryte.Select(xCoord, yCoord, candidates, blacklist);
		logDebug?.Invoke($"[Map] aetheryte {NearestAetheryte.Describe(result)}; candidates={candidates.Count}, blacklist={blacklist.Count}");
		return result;
	}

	private static IEnumerable<(uint RowId, uint TerritoryTypeId, float SizeFactor, int OffsetX, int OffsetY)> EnumerateMapParams(
		Lumina.Excel.ExcelSheet<LuminaMap> mapSheet)
	{
		foreach (var map in mapSheet)
			yield return (map.RowId, map.TerritoryType.RowId, map.SizeFactor, map.OffsetX, map.OffsetY);
	}
}
