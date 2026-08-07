#nullable enable

using System.Collections.Generic;

namespace HuntTrainAuto.Tests.HuntAlerts;

public sealed class HuntAlertsFilterTests
{
	[Fact]
	public void DefaultIntegration_is_off()
		=> Assert.False(HuntAlertsFilter.DefaultIntegration);

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
}
