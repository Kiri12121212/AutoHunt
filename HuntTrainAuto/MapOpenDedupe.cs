#nullable enable
using System;
using System.Numerics;

namespace HuntTrainAuto;

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
	/// Open when no flag, different territory, distance &gt; threshold, or <paramref name="noDuplicateFlags"/> is false.
	/// </summary>
	public static bool ShouldOpenMap(
		bool noDuplicateFlags,
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

		if (!noDuplicateFlags)
			return true;

		var linkPos = LinkPosFromRaw(linkRawX, linkRawY);
		var distance = Vector2.Distance(new Vector2(existingX, existingY), linkPos);
		return distance > duplicateDistanceThreshold;
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
