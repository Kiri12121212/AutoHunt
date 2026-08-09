#nullable enable
using System;
using System.Numerics;

namespace HuntTrainAuto.Map;

/// <summary>
/// Pure open-map / duplicate-flag decision (HTA parity). Unit-testable without AgentMap.
/// </summary>
public static class MapOpenDedupe
{
	/// <summary>HTA threshold: skip open when existing flag is within this distance of the link.</summary>
	public const float DuplicateDistanceThreshold = 10f;

	/// <summary>Map-link raw coords are milli-units; AgentMap flag floats are already scaled.</summary>
	public static Vector2 LinkPosFromRaw(int rawX, int rawY)
		=> new(rawX / 1000f, rawY / 1000f);

	/// <summary>
	/// Whether <c>OpenMapWithMapLink</c> should run.
	/// Open when no flag, different territory, or distance &gt; threshold (always skip near-dupes).
	/// </summary>
	public static bool ShouldOpenMap(
		bool isFlagMarkerSet,
		uint existingTerritoryId,
		float existingX,
		float existingY,
		uint linkTerritoryId,
		int linkRawX,
		int linkRawY,
		float duplicateDistanceThreshold = DuplicateDistanceThreshold)
	{
		if (!isFlagMarkerSet || existingTerritoryId != linkTerritoryId)
			return true;

		var linkPos = LinkPosFromRaw(linkRawX, linkRawY);
		var distance = Vector2.Distance(new Vector2(existingX, existingY), linkPos);
		return distance > duplicateDistanceThreshold;
	}

	/// <summary>Stable debug description for an evaluated open-map decision.</summary>
	public static string Describe(
		bool shouldOpen,
		bool isFlagMarkerSet,
		uint existingTerritoryId,
		uint linkTerritoryId,
		float distance,
		float duplicateDistanceThreshold = DuplicateDistanceThreshold)
	{
		if (!isFlagMarkerSet)
			return shouldOpen ? "open: no existing flag" : "skip: no existing flag";
		if (existingTerritoryId != linkTerritoryId)
			return shouldOpen
				? $"open: territory {existingTerritoryId} → {linkTerritoryId}"
				: $"skip: territory {existingTerritoryId} → {linkTerritoryId}";

		return shouldOpen
			? $"open: distance {distance:0.00} > {duplicateDistanceThreshold:0.00}"
			: $"skip duplicate: distance {distance:0.00} <= {duplicateDistanceThreshold:0.00}";
	}

	/// <summary>Light validation before calling game map APIs with chat-derived fields.</summary>
	public static bool IsPlausibleMapLink(uint territoryTypeId, uint mapId, int rawX, int rawY)
	{
		if (territoryTypeId == 0 || mapId == 0)
			return false;

		// Raw map coords are milli-units; reject absurd magnitudes from malformed payloads.
		const int maxAbsRaw = 50_000_000;
		return Math.Abs(rawX) <= maxAbsRaw && Math.Abs(rawY) <= maxAbsRaw;
	}
}
