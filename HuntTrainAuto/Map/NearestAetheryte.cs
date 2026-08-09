#nullable enable
using System;
using System.Collections.Generic;

namespace HuntTrainAuto.Map;

/// <summary>
/// Identity + map position of the nearest aetheryte (ArrivalData + same-zone path endpoints).
/// </summary>
public readonly struct NearestAetheryteResult
{
	public NearestAetheryteResult(uint rowId, string placeName, float mapX = 0f, float mapY = 0f)
	{
		RowId = rowId;
		PlaceName = placeName ?? throw new ArgumentNullException(nameof(placeName));
		MapX = mapX;
		MapY = mapY;
	}

	public uint RowId { get; }
	public string PlaceName { get; }

	/// <summary>Map X of the selected aetheryte (same units as flag map-link coords).</summary>
	public float MapX { get; }

	/// <summary>Map Y of the selected aetheryte (same units as flag map-link coords).</summary>
	public float MapY { get; }

	/// <summary>Stable debug description of the selected aetheryte.</summary>
	public string Describe()
		=> $"picked #{RowId} ({PlaceName}) at ({MapX:0.00}, {MapY:0.00})";
}

/// <summary>
/// Pure nearest-aetheryte selection by squared map distance (HTA <c>GetNearestAetheryte</c> core).
/// </summary>
public static class NearestAetheryte
{
	/// <summary>Candidate already converted to map coords (compensation applied by caller).</summary>
	public readonly struct Candidate
	{
		public Candidate(uint rowId, string placeName, float mapX, float mapY)
		{
			RowId = rowId;
			PlaceName = placeName ?? throw new ArgumentNullException(nameof(placeName));
			MapX = mapX;
			MapY = mapY;
		}

		public uint RowId { get; }
		public string PlaceName { get; }
		public float MapX { get; }
		public float MapY { get; }
	}

	/// <summary>
	/// Picks the candidate with minimum squared distance to (<paramref name="flagMapX"/>, <paramref name="flagMapY"/>),
	/// skipping blacklisted RowIds. Returns null when none remain.
	/// </summary>
	public static NearestAetheryteResult? Select(
		float flagMapX,
		float flagMapY,
		IEnumerable<Candidate> candidates,
		IReadOnlyCollection<uint>? blacklist = null)
	{
		ArgumentNullException.ThrowIfNull(candidates);

		NearestAetheryteResult? best = null;
		double bestDistance = 0;

		foreach (var candidate in candidates)
		{
			if (IsBlacklisted(blacklist, candidate.RowId))
				continue;

			var distance = Math.Pow(candidate.MapX - flagMapX, 2) + Math.Pow(candidate.MapY - flagMapY, 2);
			if (best == null || distance < bestDistance)
			{
				bestDistance = distance;
				best = new NearestAetheryteResult(
					candidate.RowId,
					candidate.PlaceName,
					candidate.MapX,
					candidate.MapY);
			}
		}

		return best;
	}

	/// <summary>Squared map distance between two map-coordinate points (HTA parity).</summary>
	public static double SquaredDistance(float x1, float y1, float x2, float y2)
		=> Math.Pow(x1 - x2, 2) + Math.Pow(y1 - y2, 2);

	/// <summary>Stable debug description of a selection result.</summary>
	public static string Describe(NearestAetheryteResult? result)
		=> result is { } selected ? selected.Describe() : "no eligible aetheryte";

	private static bool IsBlacklisted(IReadOnlyCollection<uint>? blacklist, uint rowId)
	{
		if (blacklist == null || blacklist.Count == 0)
			return false;

		foreach (var id in blacklist)
		{
			if (id == rowId)
				return true;
		}

		return false;
	}
}
