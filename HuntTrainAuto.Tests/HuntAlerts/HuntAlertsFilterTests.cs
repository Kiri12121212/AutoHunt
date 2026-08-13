#nullable enable

using System.Collections.Generic;

namespace HuntTrainAuto.Tests.HuntAlerts;

public sealed class HuntAlertsFilterTests
{
	[Fact]
	public void DefaultIntegration_is_on()
		=> Assert.True(HuntAlertsFilter.DefaultIntegration);

	[Theory]
	[InlineData(HuntAlertsFilter.HuntTypeATrain, HuntMarkRank.A)]
	[InlineData(HuntAlertsFilter.HuntTypeSRank, HuntMarkRank.S)]
	[InlineData("  new_hunt  ", HuntMarkRank.A)]
	[InlineData("  srank  ", HuntMarkRank.S)]
	public void TryMapHuntType_maps_known_types(string huntType, HuntMarkRank expected)
	{
		Assert.True(HuntAlertsFilter.TryMapHuntType(huntType, out var rank));
		Assert.Equal(expected, rank);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("B")]
	[InlineData("unknown")]
	public void TryMapHuntType_rejects_unknown(string? huntType)
	{
		Assert.False(HuntAlertsFilter.TryMapHuntType(huntType, out var rank));
		Assert.Equal(HuntMarkRank.None, rank);
	}

	[Fact]
	public void IsRankAllowed_empty_filter_accepts_A_and_S()
	{
		Assert.True(HuntAlertsFilter.IsRankAllowed(null, HuntMarkRank.A));
		Assert.True(HuntAlertsFilter.IsRankAllowed([], HuntMarkRank.S));
	}

	[Fact]
	public void IsRankAllowed_empty_filter_rejects_non_AS()
	{
		Assert.False(HuntAlertsFilter.IsRankAllowed([], HuntMarkRank.None));
		Assert.False(HuntAlertsFilter.IsRankAllowed([], HuntMarkRank.B));
	}

	[Fact]
	public void IsRankAllowed_A_only_blocks_S()
	{
		HuntMarkRank[] filter = [HuntMarkRank.A];
		Assert.True(HuntAlertsFilter.IsRankAllowed(filter, HuntMarkRank.A));
		Assert.False(HuntAlertsFilter.IsRankAllowed(filter, HuntMarkRank.S));
	}

	[Fact]
	public void IsRankAllowed_S_only_blocks_A()
	{
		HuntMarkRank[] filter = [HuntMarkRank.S];
		Assert.True(HuntAlertsFilter.IsRankAllowed(filter, HuntMarkRank.S));
		Assert.False(HuntAlertsFilter.IsRankAllowed(filter, HuntMarkRank.A));
	}

	[Fact]
	public void IsWorldBlacklisted_empty_never_blocks()
	{
		Assert.False(HuntAlertsFilter.IsWorldBlacklisted(null, "Phoenix", 21));
		Assert.False(HuntAlertsFilter.IsWorldBlacklisted([], "Phoenix", 21));
	}

	[Fact]
	public void IsWorldBlacklisted_matches_name_case_insensitive()
	{
		List<string> blacklist = ["Phoenix"];
		Assert.True(HuntAlertsFilter.IsWorldBlacklisted(blacklist, "phoenix"));
		Assert.True(HuntAlertsFilter.IsWorldBlacklisted(blacklist, "  PHOENIX  "));
		Assert.False(HuntAlertsFilter.IsWorldBlacklisted(blacklist, "Gilgamesh"));
	}

	[Fact]
	public void IsWorldBlacklisted_matches_decimal_row_id()
	{
		List<string> blacklist = ["21"];
		Assert.True(HuntAlertsFilter.IsWorldBlacklisted(blacklist, worldName: null, worldId: 21));
		Assert.False(HuntAlertsFilter.IsWorldBlacklisted(blacklist, worldName: null, worldId: 22));
		Assert.False(HuntAlertsFilter.IsWorldBlacklisted(blacklist, worldName: "Phoenix", worldId: 0));
	}

	[Fact]
	public void IsWorldBlacklisted_ignores_blank_entries()
	{
		List<string> blacklist = ["", "  ", "Phoenix"];
		Assert.True(HuntAlertsFilter.IsWorldBlacklisted(blacklist, "Phoenix"));
	}

	[Fact]
	public void ShouldAccept_requires_integration_on()
	{
		Assert.False(HuntAlertsFilter.ShouldAccept(
			huntAlertsIntegration: false,
			rankFilter: null,
			worldBlacklist: null,
			rank: HuntMarkRank.A,
			worldName: "Phoenix"));
		Assert.True(HuntAlertsFilter.ShouldAccept(
			huntAlertsIntegration: true,
			rankFilter: null,
			worldBlacklist: null,
			rank: HuntMarkRank.A,
			worldName: "Phoenix"));
	}

	[Fact]
	public void ShouldAccept_applies_rank_and_world_filters()
	{
		HuntMarkRank[] aOnly = [HuntMarkRank.A];
		List<string> blocked = ["Phoenix"];

		Assert.False(HuntAlertsFilter.ShouldAccept(
			true, aOnly, null, HuntMarkRank.S, "Gilgamesh"));
		Assert.False(HuntAlertsFilter.ShouldAccept(
			true, null, blocked, HuntMarkRank.A, "Phoenix"));
		Assert.True(HuntAlertsFilter.ShouldAccept(
			true, aOnly, blocked, HuntMarkRank.A, "Gilgamesh", worldId: 99));
	}

	[Theory]
	[InlineData(0u, HuntAlertsFilter.TrainGroups.Centurio)]
	[InlineData(1u, HuntAlertsFilter.TrainGroups.Centurio)]
	[InlineData(2u, HuntAlertsFilter.TrainGroups.Centurio)]
	[InlineData(3u, HuntAlertsFilter.TrainGroups.Shadowbringers)]
	[InlineData(4u, HuntAlertsFilter.TrainGroups.Endwalker)]
	[InlineData(5u, HuntAlertsFilter.TrainGroups.Dawntrail)]
	public void TryMapExVersion_maps_known(uint exVersion, string expected)
	{
		Assert.True(HuntAlertsFilter.TryMapExVersion(exVersion, out var group));
		Assert.Equal(expected, group);
	}

	[Fact]
	public void TryMapExVersion_rejects_unknown()
	{
		Assert.False(HuntAlertsFilter.TryMapExVersion(99, out var group));
		Assert.Equal("", group);
	}

	[Theory]
	[InlineData("Dawntrail", HuntAlertsFilter.TrainGroups.Dawntrail)]
	[InlineData("DT", HuntAlertsFilter.TrainGroups.Dawntrail)]
	[InlineData("  ew  ", HuntAlertsFilter.TrainGroups.Endwalker)]
	[InlineData("ShB", HuntAlertsFilter.TrainGroups.Shadowbringers)]
	[InlineData("Stormblood", HuntAlertsFilter.TrainGroups.Centurio)]
	public void TryNormalizeTrainGroup_maps_aliases(string raw, string expected)
	{
		Assert.True(HuntAlertsFilter.TryNormalizeTrainGroup(raw, out var group));
		Assert.Equal(expected, group);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("UnknownExpansion")]
	public void TryNormalizeTrainGroup_rejects_unknown(string? raw)
	{
		Assert.False(HuntAlertsFilter.TryNormalizeTrainGroup(raw, out var group));
		Assert.Equal("", group);
	}

	[Fact]
	public void TryResolveTrainGroup_prefers_exVersion_over_huntKind()
	{
		Assert.True(HuntAlertsFilter.TryResolveTrainGroup(
			"Endwalker",
			HuntAlertsFilter.ExVersionDawntrail,
			out var group));
		Assert.Equal(HuntAlertsFilter.TrainGroups.Dawntrail, group);
	}

	[Fact]
	public void TryResolveTrainGroup_falls_back_to_huntKind()
	{
		Assert.True(HuntAlertsFilter.TryResolveTrainGroup(
			"DT",
			exVersion: null,
			out var group));
		Assert.Equal(HuntAlertsFilter.TrainGroups.Dawntrail, group);
	}

	[Fact]
	public void IsTrainGroupAllowed_empty_accepts_all()
	{
		Assert.True(HuntAlertsFilter.IsTrainGroupAllowed(null, "Endwalker", 4));
		Assert.True(HuntAlertsFilter.IsTrainGroupAllowed([], "whatever", null));
	}

	[Fact]
	public void IsTrainGroupAllowed_dawntrail_only()
	{
		string[] dtOnly = [HuntAlertsFilter.TrainGroups.Dawntrail];
		Assert.True(HuntAlertsFilter.IsTrainGroupAllowed(
			dtOnly, huntKind: "EW", exVersion: HuntAlertsFilter.ExVersionDawntrail));
		Assert.False(HuntAlertsFilter.IsTrainGroupAllowed(
			dtOnly, huntKind: "Endwalker", exVersion: HuntAlertsFilter.ExVersionEndwalker));
		Assert.True(HuntAlertsFilter.IsTrainGroupAllowed(
			dtOnly, huntKind: "Dawntrail", exVersion: null));
		Assert.False(HuntAlertsFilter.IsTrainGroupAllowed(
			dtOnly, huntKind: "unknown", exVersion: null));
	}

	[Fact]
	public void ShouldAccept_applies_train_group_filter()
	{
		string[] dtOnly = [HuntAlertsFilter.TrainGroups.Dawntrail];
		Assert.False(HuntAlertsFilter.ShouldAccept(
			true,
			null,
			null,
			HuntMarkRank.A,
			"Phoenix",
			trainGroupFilter: dtOnly,
			huntKind: "Endwalker",
			exVersion: HuntAlertsFilter.ExVersionEndwalker));
		Assert.True(HuntAlertsFilter.ShouldAccept(
			true,
			[HuntMarkRank.A],
			null,
			HuntMarkRank.A,
			"Phoenix",
			trainGroupFilter: dtOnly,
			huntKind: "DT",
			exVersion: HuntAlertsFilter.ExVersionDawntrail));
	}

	[Fact]
	public void SetRankFilterEnabled_both_clears_list()
	{
		var filter = new List<HuntMarkRank> { HuntMarkRank.A };
		HuntAlertsFilter.SetRankFilterEnabled(filter, HuntMarkRank.S, enabled: true);
		Assert.Empty(filter);
		Assert.True(HuntAlertsFilter.IsRankFilterEnabled(filter, HuntMarkRank.A));
		Assert.True(HuntAlertsFilter.IsRankFilterEnabled(filter, HuntMarkRank.S));
	}

	[Fact]
	public void SetRankFilterEnabled_a_only()
	{
		var filter = new List<HuntMarkRank>();
		HuntAlertsFilter.SetRankFilterEnabled(filter, HuntMarkRank.S, enabled: false);
		Assert.Equal([HuntMarkRank.A], filter);
	}

	[Fact]
	public void SetRankFilterEnabled_none_uses_sentinel()
	{
		var filter = new List<HuntMarkRank> { HuntMarkRank.A };
		HuntAlertsFilter.SetRankFilterEnabled(filter, HuntMarkRank.A, enabled: false);
		Assert.Equal([HuntMarkRank.None], filter);
		Assert.False(HuntAlertsFilter.IsRankFilterEnabled(filter, HuntMarkRank.A));
		Assert.False(HuntAlertsFilter.IsRankFilterEnabled(filter, HuntMarkRank.S));
		Assert.False(HuntAlertsFilter.IsRankAllowed(filter, HuntMarkRank.A));
	}

	[Fact]
	public void SetTrainGroupFilterEnabled_all_clears_list()
	{
		var filter = new List<string> { HuntAlertsFilter.TrainGroups.Dawntrail };
		foreach (var g in HuntAlertsFilter.TrainGroups.All)
			HuntAlertsFilter.SetTrainGroupFilterEnabled(filter, g, enabled: true);
		Assert.Empty(filter);
	}

	[Fact]
	public void SetTrainGroupFilterEnabled_none_uses_sentinel()
	{
		var filter = new List<string> { HuntAlertsFilter.TrainGroups.Dawntrail };
		HuntAlertsFilter.SetTrainGroupFilterEnabled(
			filter,
			HuntAlertsFilter.TrainGroups.Dawntrail,
			enabled: false);
		Assert.Equal([HuntAlertsFilter.TrainGroupNoneSentinel], filter);
		Assert.False(HuntAlertsFilter.IsTrainGroupFilterEnabled(
			filter,
			HuntAlertsFilter.TrainGroups.Dawntrail));
		Assert.False(HuntAlertsFilter.IsTrainGroupAllowed(
			filter,
			HuntAlertsFilter.TrainGroups.Dawntrail,
			HuntAlertsFilter.ExVersionDawntrail));
	}

	[Fact]
	public void SetTrainGroupFilterEnabled_dawntrail_only()
	{
		var filter = new List<string>();
		foreach (var g in HuntAlertsFilter.TrainGroups.All)
		{
			if (g != HuntAlertsFilter.TrainGroups.Dawntrail)
				HuntAlertsFilter.SetTrainGroupFilterEnabled(filter, g, enabled: false);
		}

		Assert.Equal([HuntAlertsFilter.TrainGroups.Dawntrail], filter);
	}
}
