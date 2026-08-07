#nullable enable

using System;
using System.Collections.Generic;

namespace HuntTrainAuto.HuntAlerts;

/// <summary>
/// Pure gates for optional HuntAlerts IPC intake (TASKS 10.1).
/// No logging — callers decide telemetry.
/// </summary>
public static class HuntAlertsFilter
{
	/// <summary>Default for <c>Configuration.HuntAlertsIntegration</c> (feature off).</summary>
	public const bool DefaultIntegration = false;

	/// <summary>HuntAlerts <c>huntType</c> for A-train start.</summary>
	public const string HuntTypeATrain = "new_hunt";

	/// <summary>HuntAlerts <c>huntType</c> for S-rank world visit.</summary>
	public const string HuntTypeSRank = "srank";

	/// <summary>
	/// Map HuntAlerts <c>huntType</c> to <see cref="HuntMarkRank"/>.
	/// Returns false for unknown / blank types.
	/// </summary>
	public static bool TryMapHuntType(string? huntType, out HuntMarkRank rank)
	{
		rank = huntType?.Trim() switch
		{
			HuntTypeATrain => HuntMarkRank.A,
			HuntTypeSRank => HuntMarkRank.S,
			_ => HuntMarkRank.None,
		};
		return rank is HuntMarkRank.A or HuntMarkRank.S;
	}

	/// <summary>
	/// Empty / null filter accepts A and S. Otherwise the rank must appear in the list.
	/// Non A/S ranks are never accepted.
	/// </summary>
	public static bool IsRankAllowed(IReadOnlyList<HuntMarkRank>? rankFilter, HuntMarkRank rank)
	{
		if (rank is not (HuntMarkRank.A or HuntMarkRank.S))
			return false;

		if (rankFilter == null || rankFilter.Count == 0)
			return true;

		for (var i = 0; i < rankFilter.Count; i++)
		{
			if (rankFilter[i] == rank)
				return true;
		}

		return false;
	}

	/// <summary>
	/// Empty / null blacklist never blocks. Entries may be world names (case-insensitive)
	/// or decimal world RowIds.
	/// </summary>
	public static bool IsWorldBlacklisted(
		IReadOnlyList<string>? blacklist,
		string? worldName,
		uint worldId = 0)
	{
		if (blacklist == null || blacklist.Count == 0)
			return false;

		var trimmedName = worldName?.Trim();
		var idText = worldId == 0 ? null : worldId.ToString();

		for (var i = 0; i < blacklist.Count; i++)
		{
			var entry = blacklist[i]?.Trim();
			if (string.IsNullOrEmpty(entry))
				continue;

			if (idText != null && string.Equals(entry, idText, StringComparison.Ordinal))
				return true;

			if (!string.IsNullOrEmpty(trimmedName)
			    && string.Equals(entry, trimmedName, StringComparison.OrdinalIgnoreCase))
				return true;
		}

		return false;
	}

	/// <summary>
	/// Master gate: integration on, rank allowed, world not blacklisted.
	/// </summary>
	public static bool ShouldAccept(
		bool huntAlertsIntegration,
		IReadOnlyList<HuntMarkRank>? rankFilter,
		IReadOnlyList<string>? worldBlacklist,
		HuntMarkRank rank,
		string? worldName,
		uint worldId = 0)
	{
		if (!huntAlertsIntegration)
			return false;
		if (!IsRankAllowed(rankFilter, rank))
			return false;
		if (IsWorldBlacklisted(worldBlacklist, worldName, worldId))
			return false;
		return true;
	}
}
