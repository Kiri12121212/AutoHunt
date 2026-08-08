#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using HuntTrainAuto.Domain;
using HuntTrainAuto.Map;

namespace HuntTrainAuto.HuntAlerts;

/// <summary>
/// Pure <see cref="HuntTrainMessage"/> → <see cref="HuntFlag"/> / <see cref="ArrivalData"/>
/// mapping (TASKS 10.3). No Excel / CallGate — callers supply map sheet params when known.
/// </summary>
public static class HuntTrainMessageMapper
{
	/// <summary>Dalamud <c>MapLinkPayload</c> default fudge for float→raw.</summary>
	public const float DefaultMapCoordFudge = 0.05f;

	/// <summary>Fallback <c>Map.SizeFactor</c> when sheets are unavailable.</summary>
	public const float DefaultSizeFactor = 100f;

	/// <summary>
	/// Unpack sheet-resolved params for <see cref="TryMap"/>, or defaults when
	/// <paramref name="resolved"/> is null (sheets missing / territory unknown).
	/// </summary>
	public static void UnpackMapParams(
		MapCoordParams? resolved,
		out uint mapId,
		out float sizeFactor,
		out int offsetX,
		out int offsetY)
	{
		if (resolved is { } p)
		{
			mapId = p.MapId;
			sizeFactor = p.SizeFactor > 0f ? p.SizeFactor : DefaultSizeFactor;
			offsetX = p.OffsetX;
			offsetY = p.OffsetY;
			return;
		}

		mapId = 0;
		sizeFactor = DefaultSizeFactor;
		offsetX = 0;
		offsetY = 0;
	}

	/// <summary>
	/// Map a HuntAlerts train message to a chat-shaped <see cref="HuntFlag"/>.
	/// Returns false when filtered, incomplete, or coordinates cannot be resolved.
	/// Attaches <see cref="ArrivalData"/> when <c>startLocationAetheryteId</c> is non-zero.
	/// Always sets <see cref="HuntFlag.HuntWorld"/> from <c>huntWorld</c> when non-blank
	/// so cross-world visit works without Arrival.
	/// Callers should resolve <paramref name="mapId"/> / size / offsets via
	/// <c>MapManager.GetMapParams</c> (or <see cref="UnpackMapParams"/>) when sheets are available.
	/// </summary>
	public static bool TryMap(
		HuntTrainMessage? message,
		bool huntAlertsIntegration,
		IReadOnlyList<HuntMarkRank>? rankFilter,
		IReadOnlyList<string>? worldBlacklist,
		out HuntFlag flag,
		uint mapId = 0,
		float sizeFactor = DefaultSizeFactor,
		int offsetX = 0,
		int offsetY = 0,
		DateTimeOffset? timestamp = null,
		float mapCoordFudge = DefaultMapCoordFudge,
		IReadOnlyList<string>? trainGroupFilter = null,
		uint? expansionVersion = null)
		=> TryMap(
			message,
			huntAlertsIntegration,
			rankFilter,
			worldBlacklist,
			out flag,
			out _,
			mapId,
			sizeFactor,
			offsetX,
			offsetY,
			timestamp,
			mapCoordFudge,
			trainGroupFilter,
			expansionVersion);

	/// <summary>
	/// Same as <see cref="TryMap(HuntTrainMessage?,bool,IReadOnlyList{HuntMarkRank}?,IReadOnlyList{string}?,out HuntFlag,uint,float,int,int,DateTimeOffset?,float,IReadOnlyList{string}?,uint?)"/>
	/// but reports a short <paramref name="rejectReason"/> when mapping fails (for logs / UI).
	/// </summary>
	public static bool TryMap(
		HuntTrainMessage? message,
		bool huntAlertsIntegration,
		IReadOnlyList<HuntMarkRank>? rankFilter,
		IReadOnlyList<string>? worldBlacklist,
		out HuntFlag flag,
		out string rejectReason,
		uint mapId = 0,
		float sizeFactor = DefaultSizeFactor,
		int offsetX = 0,
		int offsetY = 0,
		DateTimeOffset? timestamp = null,
		float mapCoordFudge = DefaultMapCoordFudge,
		IReadOnlyList<string>? trainGroupFilter = null,
		uint? expansionVersion = null)
	{
		flag = null!;
		rejectReason = "";
		if (message == null)
		{
			rejectReason = "null message";
			return false;
		}

		if (!HuntAlertsFilter.TryMapHuntType(message.huntType, out var rank))
		{
			rejectReason = string.IsNullOrWhiteSpace(message.huntType)
				? "missing huntType"
				: $"unknown huntType '{message.huntType.Trim()}'";
			return false;
		}

		if (!HuntAlertsFilter.ShouldAccept(
			    huntAlertsIntegration,
			    rankFilter,
			    worldBlacklist,
			    rank,
			    message.huntWorld,
			    trainGroupFilter: trainGroupFilter,
			    huntKind: message.huntKind,
			    exVersion: expansionVersion))
		{
			if (!huntAlertsIntegration)
				rejectReason = "integration off";
			else if (!HuntAlertsFilter.IsRankAllowed(rankFilter, rank))
				rejectReason = $"rank filter blocked {rank}";
			else if (HuntAlertsFilter.IsWorldBlacklisted(worldBlacklist, message.huntWorld))
				rejectReason = $"world blacklisted '{message.huntWorld}'";
			else if (!HuntAlertsFilter.IsTrainGroupAllowed(
				         trainGroupFilter, message.huntKind, expansionVersion))
				rejectReason = $"expansion filter blocked kind='{message.huntKind}' ex={expansionVersion?.ToString() ?? "?"}";
			else
				rejectReason = "filtered";
			return false;
		}

		if (message.startTerritoryTypeId == 0)
		{
			rejectReason = "missing startTerritoryTypeId";
			return false;
		}

		if (!TryResolveMapCoords(message, out var mapX, out var mapY))
		{
			rejectReason = "missing map coords";
			return false;
		}

		if (sizeFactor <= 0f)
			sizeFactor = DefaultSizeFactor;

		var rawX = MapCoordinates.ConvertMapCoordinateToRawPosition(
			mapX + mapCoordFudge, sizeFactor, offsetX);
		var rawY = MapCoordinates.ConvertMapCoordinateToRawPosition(
			mapY + mapCoordFudge, sizeFactor, offsetY);

		var placeName = FirstNonBlank(message.startLocation, message.startZone);

		flag = HuntFlag.FromMapLink(
			message.startTerritoryTypeId,
			mapId,
			rawX,
			rawY,
			placeName,
			timestamp);

		// Carry hunt world on the flag even when Arrival is omitted (aetheryte id 0).
		var huntWorld = message.huntWorld?.Trim();
		flag.HuntWorld = string.IsNullOrEmpty(huntWorld) ? null : huntWorld;

		flag.Arrival = ArrivalData.CreateOrNull(
			message.startLocationAetheryteId,
			message.startTerritoryTypeId,
			message.instance,
			message.huntWorld);

		return true;
	}

	/// <summary>
	/// Prefer <c>mapLocationX|Y</c> when set; else parse <c>locationCoords</c> as
	/// <c>"x, y"</c> (HTA <c>SonarMonitor</c> split).
	/// </summary>
	public static bool TryResolveMapCoords(
		HuntTrainMessage message,
		out float mapX,
		out float mapY)
	{
		ArgumentNullException.ThrowIfNull(message);

		if (message.mapLocationX != 0f || message.mapLocationY != 0f)
		{
			mapX = message.mapLocationX;
			mapY = message.mapLocationY;
			return true;
		}

		return TryParseLocationCoords(message.locationCoords, out mapX, out mapY);
	}

	/// <summary>Parse HuntAlerts <c>locationCoords</c> (<c>"x, y"</c> / flexible separators).</summary>
	public static bool TryParseLocationCoords(string? locationCoords, out float mapX, out float mapY)
	{
		mapX = 0f;
		mapY = 0f;
		if (string.IsNullOrWhiteSpace(locationCoords))
			return false;

		var parts = locationCoords.Split(',', StringSplitOptions.TrimEntries);
		if (parts.Length < 2)
			return false;

		if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out mapX))
			return false;
		if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out mapY))
			return false;

		return true;
	}

	private static string? FirstNonBlank(string? primary, string? fallback)
	{
		var a = primary?.Trim();
		if (!string.IsNullOrEmpty(a))
			return a;
		var b = fallback?.Trim();
		return string.IsNullOrEmpty(b) ? null : b;
	}
}
