#nullable enable
using System;
using System.Collections.Generic;

namespace HuntTrainAuto;

/// <summary>Identity of the nearest aetheryte (usable by later ArrivalData wiring).</summary>
public readonly struct NearestAetheryteResult
{
	public NearestAetheryteResult(uint rowId, string placeName)
	{
		RowId = rowId;
		PlaceName = placeName ?? throw new ArgumentNullException(nameof(placeName));
	}

	public uint RowId { get; }
	public string PlaceName { get; }
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
				best = new NearestAetheryteResult(candidate.RowId, candidate.PlaceName);
			}
		}

		return best;
	}

	/// <summary>Squared map distance between two map-coordinate points (HTA parity).</summary>
	public static double SquaredDistance(float x1, float y1, float x2, float y2)
		=> Math.Pow(x1 - x2, 2) + Math.Pow(y1 - y2, 2);

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
