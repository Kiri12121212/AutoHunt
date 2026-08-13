#nullable enable

using System;
using System.Collections.Generic;
using HuntTrainAuto.Domain;

namespace HuntTrainAuto.HuntAlerts;

/// <summary>
/// Pure gates for optional HuntAlerts IPC intake (TASKS 10.1).
/// No logging — callers decide telemetry.
/// </summary>
public static class HuntAlertsFilter
{
	/// <summary>Default for <c>Configuration.HuntAlertsIntegration</c> (feature on).</summary>
	public const bool DefaultIntegration = true;

	/// <summary>HuntAlerts <c>huntType</c> for A-train start.</summary>
	public const string HuntTypeATrain = "new_hunt";

	/// <summary>HuntAlerts <c>huntType</c> for S-rank world visit.</summary>
	public const string HuntTypeSRank = "srank";

	/// <summary>TerritoryType.ExVersion RowId for Dawntrail.</summary>
	public const uint ExVersionDawntrail = 5;

	/// <summary>TerritoryType.ExVersion RowId for Endwalker.</summary>
	public const uint ExVersionEndwalker = 4;

	/// <summary>TerritoryType.ExVersion RowId for Shadowbringers.</summary>
	public const uint ExVersionShadowbringers = 3;

	/// <summary>
	/// Canonical train-group names (HuntAlerts <c>HuntGroups</c> parity).
	/// Used by <see cref="Configuration.HuntAlertsTrainGroupFilter"/>.
	/// </summary>
	public static class TrainGroups
	{
		public const string Centurio = "Centurio";
		public const string Shadowbringers = "Shadowbringers";
		public const string Endwalker = "Endwalker";
		public const string Dawntrail = "Dawntrail";

		public static readonly string[] All =
		[
			Centurio,
			Shadowbringers,
			Endwalker,
			Dawntrail,
		];
	}

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
	/// Map <c>TerritoryType.ExVersion</c> RowId to a HuntAlerts train group.
	/// ARR/HW/SB → Centurio (HuntAlerts <c>NormalizeGroup</c> parity).
	/// </summary>
	public static bool TryMapExVersion(uint exVersion, out string trainGroup)
	{
		trainGroup = exVersion switch
		{
			0 or 1 or 2 => TrainGroups.Centurio,
			ExVersionShadowbringers => TrainGroups.Shadowbringers,
			ExVersionEndwalker => TrainGroups.Endwalker,
			ExVersionDawntrail => TrainGroups.Dawntrail,
			_ => "",
		};
		return trainGroup.Length > 0;
	}

	/// <summary>
	/// Normalize HuntAlerts <c>huntKind</c> (and aliases) to a canonical train group.
	/// Mirrors HuntAlerts <c>HuntAlertFilter.NormalizeGroup</c>.
	/// </summary>
	public static bool TryNormalizeTrainGroup(string? huntKind, out string trainGroup)
	{
		trainGroup = "";
		if (string.IsNullOrWhiteSpace(huntKind))
			return false;

		trainGroup = huntKind.Trim().ToUpperInvariant() switch
		{
			"DAWNTRAIL" or "DT" => TrainGroups.Dawntrail,
			"ENDWALKER" or "EW" => TrainGroups.Endwalker,
			"SHADOWBRINGERS" or "SHB" => TrainGroups.Shadowbringers,
			"CENTURIO" => TrainGroups.Centurio,
			"STORMBLOOD" or "SB" => TrainGroups.Centurio,
			"HEAVENSWARD" or "HW" => TrainGroups.Centurio,
			"ARR" or "A REALM REBORN" or "REALM REBORN" => TrainGroups.Centurio,
			_ => "",
		};
		return trainGroup.Length > 0;
	}

	/// <summary>
	/// Resolve the expansion group for a message: prefer sheet
	/// <paramref name="exVersion"/> when known, else <paramref name="huntKind"/>.
	/// </summary>
	public static bool TryResolveTrainGroup(
		string? huntKind,
		uint? exVersion,
		out string trainGroup)
	{
		if (exVersion is { } ver && TryMapExVersion(ver, out trainGroup))
			return true;

		return TryNormalizeTrainGroup(huntKind, out trainGroup);
	}

	/// <summary>
	/// Empty / null filter accepts all expansions. Otherwise the resolved group must
	/// appear in the list (canonical names; case-insensitive).
	/// Unknown expansion (no ExVersion and unparseable huntKind) is rejected when a
	/// filter is set.
	/// </summary>
	public static bool IsTrainGroupAllowed(
		IReadOnlyList<string>? trainGroupFilter,
		string? huntKind,
		uint? exVersion = null)
	{
		if (trainGroupFilter == null || trainGroupFilter.Count == 0)
			return true;

		if (!TryResolveTrainGroup(huntKind, exVersion, out var group))
			return false;

		for (var i = 0; i < trainGroupFilter.Count; i++)
		{
			var entry = trainGroupFilter[i]?.Trim();
			if (string.IsNullOrEmpty(entry))
				continue;

			if (string.Equals(entry, group, StringComparison.OrdinalIgnoreCase))
				return true;
		}

		return false;
	}

	/// <summary>
	/// Sentinel in <see cref="Configuration.HuntAlertsTrainGroupFilter"/> meaning accept none.
	/// Empty list still means accept all.
	/// </summary>
	public const string TrainGroupNoneSentinel = "#none";

	/// <summary>
	/// Rank checkbox helper: empty filter means all ranks allowed.
	/// <see cref="HuntMarkRank.None"/>-only list means accept none.
	/// </summary>
	public static bool IsRankFilterEnabled(IReadOnlyList<HuntMarkRank>? rankFilter, HuntMarkRank rank)
	{
		if (rankFilter == null || rankFilter.Count == 0)
			return true;

		for (var i = 0; i < rankFilter.Count; i++)
		{
			var entry = rankFilter[i];
			if (entry is not (HuntMarkRank.A or HuntMarkRank.S))
				continue;
			if (entry == rank)
				return true;
		}

		return false;
	}

	/// <summary>
	/// Mutate a rank allowlist from a checkbox. Both allowed → clear list (accept all).
	/// Neither allowed → <see cref="HuntMarkRank.None"/> sentinel (accept none).
	/// </summary>
	public static void SetRankFilterEnabled(List<HuntMarkRank> rankFilter, HuntMarkRank rank, bool enabled)
	{
		ArgumentNullException.ThrowIfNull(rankFilter);
		if (rank is not (HuntMarkRank.A or HuntMarkRank.S))
			return;

		var a = IsRankFilterEnabled(rankFilter, HuntMarkRank.A);
		var s = IsRankFilterEnabled(rankFilter, HuntMarkRank.S);
		if (rank == HuntMarkRank.A)
			a = enabled;
		else
			s = enabled;

		rankFilter.Clear();
		if (a && s)
			return;
		if (!a && !s)
		{
			rankFilter.Add(HuntMarkRank.None);
			return;
		}

		if (a)
			rankFilter.Add(HuntMarkRank.A);
		if (s)
			rankFilter.Add(HuntMarkRank.S);
	}

	/// <summary>
	/// Train-group checkbox helper: empty filter means all groups allowed.
	/// <see cref="TrainGroupNoneSentinel"/>-only list means accept none.
	/// </summary>
	public static bool IsTrainGroupFilterEnabled(
		IReadOnlyList<string>? trainGroupFilter,
		string trainGroup)
	{
		if (trainGroupFilter == null || trainGroupFilter.Count == 0)
			return true;

		for (var i = 0; i < trainGroupFilter.Count; i++)
		{
			var entry = trainGroupFilter[i]?.Trim();
			if (string.IsNullOrEmpty(entry)
			    || string.Equals(entry, TrainGroupNoneSentinel, StringComparison.Ordinal))
				continue;

			if (string.Equals(entry, trainGroup, StringComparison.OrdinalIgnoreCase))
				return true;
		}

		return false;
	}

	/// <summary>
	/// Mutate a train-group allowlist from a checkbox. All groups allowed → clear list.
	/// None allowed → <see cref="TrainGroupNoneSentinel"/> (accept none).
	/// </summary>
	public static void SetTrainGroupFilterEnabled(
		List<string> trainGroupFilter,
		string trainGroup,
		bool enabled)
	{
		ArgumentNullException.ThrowIfNull(trainGroupFilter);
		if (string.IsNullOrWhiteSpace(trainGroup))
			return;

		var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		if (trainGroupFilter.Count == 0)
		{
			foreach (var g in TrainGroups.All)
				selected.Add(g);
		}
		else
		{
			foreach (var entry in trainGroupFilter)
			{
				var t = entry?.Trim();
				if (string.IsNullOrEmpty(t)
				    || string.Equals(t, TrainGroupNoneSentinel, StringComparison.Ordinal))
					continue;
				selected.Add(t);
			}
		}

		if (enabled)
			selected.Add(trainGroup);
		else
			selected.Remove(trainGroup);

		trainGroupFilter.Clear();
		if (selected.Count >= TrainGroups.All.Length)
			return;

		if (selected.Count == 0)
		{
			trainGroupFilter.Add(TrainGroupNoneSentinel);
			return;
		}

		foreach (var g in TrainGroups.All)
		{
			if (selected.Contains(g))
				trainGroupFilter.Add(g);
		}
	}

	/// <summary>
	/// Master gate: integration on, rank allowed, world not blacklisted,
	/// train group allowed (ExVersion preferred over huntKind).
	/// </summary>
	public static string DescribeAcceptance(
		bool huntAlertsIntegration,
		IReadOnlyList<HuntMarkRank>? rankFilter,
		IReadOnlyList<string>? worldBlacklist,
		HuntMarkRank rank,
		string? worldName,
		uint worldId = 0,
		IReadOnlyList<string>? trainGroupFilter = null,
		string? huntKind = null,
		uint? exVersion = null)
	{
		if (!huntAlertsIntegration)
			return "rejected: integration off";
		if (!IsRankAllowed(rankFilter, rank))
			return $"rejected: rank filter blocked {rank}";
		if (IsWorldBlacklisted(worldBlacklist, worldName, worldId))
			return $"rejected: world blacklisted '{worldName?.Trim() ?? "?"}'";
		if (!IsTrainGroupAllowed(trainGroupFilter, huntKind, exVersion))
			return $"rejected: expansion filter blocked kind='{huntKind?.Trim() ?? "?"}' ex={exVersion?.ToString() ?? "?"}";
		return $"accepted: rank={rank} world={worldName?.Trim() ?? "?"}";
	}

	public static bool ShouldAccept(
		bool huntAlertsIntegration,
		IReadOnlyList<HuntMarkRank>? rankFilter,
		IReadOnlyList<string>? worldBlacklist,
		HuntMarkRank rank,
		string? worldName,
		uint worldId = 0,
		IReadOnlyList<string>? trainGroupFilter = null,
		string? huntKind = null,
		uint? exVersion = null)
	{
		if (!huntAlertsIntegration)
			return false;
		if (!IsRankAllowed(rankFilter, rank))
			return false;
		if (IsWorldBlacklisted(worldBlacklist, worldName, worldId))
			return false;
		if (!IsTrainGroupAllowed(trainGroupFilter, huntKind, exVersion))
			return false;
		return true;
	}
}
