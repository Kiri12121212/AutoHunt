#nullable enable
using System;
using System.Collections.Generic;

namespace HuntTrainAuto.PartyFinder;

/// <summary>
/// Snapshot of a Party Finder listing used by pure match helpers (no Dalamud types).
/// </summary>
public readonly struct HuntPfListingInfo
{
	public required ulong Id { get; init; }
	public required uint CategoryFlags { get; init; }
	public required byte SlotsFilled { get; init; }
	public required byte SlotsAvailable { get; init; }
	public required byte SearchAreaFlags { get; init; }
	public required string Description { get; init; }
	public required string LeaderName { get; init; }
}

/// <summary>
/// Heuristics for identifying a joinable hunt-train Party Finder listing.
/// Category <c>TheHunt</c> (ClientStructs / Dalamud DutyCategory bit 11) is primary;
/// description keywords are a soft boost only.
/// </summary>
public static class HuntPfMatch
{
	/// <summary>AgentLookingForGroup.DutyCategory.TheHunt / Dalamud DutyCategory.TheHunt.</summary>
	public const uint TheHuntCategory = 1u << 11;

	/// <summary>JoinCondition / SearchAreaFlags.Private.</summary>
	public const byte PrivateSearchArea = 1 << 1;

	/// <summary>PF CategoryTab index for The Hunt (0 = All … 16 = Other).</summary>
	public const byte HuntCategoryTab = 11;

	/// <summary>SearchAreaTab: World (hunt credit / same-world trains).</summary>
	public const byte WorldSearchAreaTab = 1;

	private static readonly string[] DescriptionBoostKeywords =
	[
		"train",
		"hunt",
		"a-rank",
		"arank",
		"a rank",
		"marks",
		"hunt train",
	];

	/// <summary>
	/// True when the listing is under The Hunt, has an open slot, and is not private.
	/// </summary>
	public static bool IsSuitable(in HuntPfListingInfo listing)
	{
		if (listing.Id == 0)
			return false;
		if ((listing.CategoryFlags & TheHuntCategory) == 0)
			return false;
		if ((listing.SearchAreaFlags & PrivateSearchArea) != 0)
			return false;
		if (listing.SlotsAvailable == 0)
			return false;
		return listing.SlotsFilled < listing.SlotsAvailable;
	}

	/// <summary>Soft score: fuller parties first, then keyword boosts in the comment.</summary>
	public static int Score(in HuntPfListingInfo listing)
	{
		if (!IsSuitable(in listing))
			return int.MinValue;

		var score = listing.SlotsFilled * 10;
		var desc = listing.Description;
		if (!string.IsNullOrEmpty(desc))
		{
			foreach (var kw in DescriptionBoostKeywords)
			{
				if (desc.Contains(kw, StringComparison.OrdinalIgnoreCase))
					score += 3;
			}
		}

		return score;
	}

	/// <summary>
	/// Pick the best suitable listing, or null when none qualify.
	/// </summary>
	public static HuntPfListingInfo? PickBest(IReadOnlyList<HuntPfListingInfo> listings)
	{
		if (listings == null || listings.Count == 0)
			return null;

		HuntPfListingInfo? best = null;
		var bestScore = int.MinValue;
		for (var i = 0; i < listings.Count; i++)
		{
			var listing = listings[i];
			var score = Score(in listing);
			if (score < 0)
				continue;
			if (best is null || score > bestScore || (score == bestScore && listing.Id < best.Value.Id))
			{
				best = listing;
				bestScore = score;
			}
		}

		return best;
	}
}
