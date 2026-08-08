#nullable enable

namespace HuntTrainAuto.Tests.PartyFinder;

public sealed class HuntPfDecisionTests
{
	[Fact]
	public void Decide_none_when_disabled()
		=> Assert.Equal(
			HuntPfKind.None,
			HuntPfDecision.Decide(
				enabled: false,
				atHuntStart: true,
				inParty: false,
				joinedLatch: false,
				hasSuitableListing: true,
				detailReadyToJoin: true,
				pluginOpenedListing: true,
				actionReady: true));

	[Fact]
	public void Decide_none_when_not_at_hunt_start()
		=> Assert.Equal(
			HuntPfKind.None,
			HuntPfDecision.Decide(
				enabled: true,
				atHuntStart: false,
				inParty: false,
				joinedLatch: false,
				hasSuitableListing: true,
				detailReadyToJoin: false,
				pluginOpenedListing: false,
				actionReady: true));

	[Fact]
	public void Decide_none_when_in_combat()
		=> Assert.Equal(
			HuntPfKind.None,
			HuntPfDecision.Decide(
				enabled: true,
				atHuntStart: true,
				inCombat: true,
				inParty: false,
				joinedLatch: false,
				hasSuitableListing: true,
				detailReadyToJoin: true,
				pluginOpenedListing: true,
				actionReady: true));

	[Fact]
	public void Decide_none_when_already_in_party()
		=> Assert.Equal(
			HuntPfKind.None,
			HuntPfDecision.Decide(
				enabled: true,
				atHuntStart: true,
				inParty: true,
				joinedLatch: false,
				hasSuitableListing: true,
				detailReadyToJoin: true,
				pluginOpenedListing: true,
				actionReady: true));

	[Fact]
	public void Decide_none_when_joined_latch_held()
		=> Assert.Equal(
			HuntPfKind.None,
			HuntPfDecision.Decide(
				enabled: true,
				atHuntStart: true,
				inParty: false,
				joinedLatch: true,
				hasSuitableListing: true,
				detailReadyToJoin: true,
				pluginOpenedListing: true,
				actionReady: true));

	[Fact]
	public void Decide_none_while_throttle_pending()
		=> Assert.Equal(
			HuntPfKind.None,
			HuntPfDecision.Decide(
				enabled: true,
				atHuntStart: true,
				inParty: false,
				joinedLatch: false,
				hasSuitableListing: false,
				detailReadyToJoin: false,
				pluginOpenedListing: false,
				actionReady: false));

	[Fact]
	public void Decide_refresh_when_no_listing()
		=> Assert.Equal(
			HuntPfKind.RefreshListings,
			HuntPfDecision.Decide(
				enabled: true,
				atHuntStart: true,
				inParty: false,
				joinedLatch: false,
				hasSuitableListing: false,
				detailReadyToJoin: false,
				pluginOpenedListing: false,
				actionReady: true));

	[Fact]
	public void Decide_open_when_listing_available()
		=> Assert.Equal(
			HuntPfKind.OpenListing,
			HuntPfDecision.Decide(
				enabled: true,
				atHuntStart: true,
				inParty: false,
				joinedLatch: false,
				hasSuitableListing: true,
				detailReadyToJoin: false,
				pluginOpenedListing: false,
				actionReady: true));

	[Fact]
	public void Decide_click_join_when_detail_ready_and_plugin_opened()
		=> Assert.Equal(
			HuntPfKind.ClickJoin,
			HuntPfDecision.Decide(
				enabled: true,
				atHuntStart: true,
				inParty: false,
				joinedLatch: false,
				hasSuitableListing: true,
				detailReadyToJoin: true,
				pluginOpenedListing: true,
				actionReady: true));

	[Fact]
	public void Decide_open_not_click_when_detail_ready_but_not_plugin_opened()
		=> Assert.Equal(
			HuntPfKind.OpenListing,
			HuntPfDecision.Decide(
				enabled: true,
				atHuntStart: true,
				inParty: false,
				joinedLatch: false,
				hasSuitableListing: true,
				detailReadyToJoin: true,
				pluginOpenedListing: false,
				actionReady: true));

	[Fact]
	public void Decide_refresh_when_detail_ready_without_suitable_listing()
		=> Assert.Equal(
			HuntPfKind.RefreshListings,
			HuntPfDecision.Decide(
				enabled: true,
				atHuntStart: true,
				inParty: false,
				joinedLatch: false,
				hasSuitableListing: false,
				detailReadyToJoin: true,
				pluginOpenedListing: true,
				actionReady: true));

	[Fact]
	public void NextJoinedLatch_sets_on_in_party()
	{
		Assert.False(HuntPfDecision.NextJoinedLatch(inParty: false, joinedLatch: false));
		Assert.True(HuntPfDecision.NextJoinedLatch(inParty: true, joinedLatch: false));
		Assert.True(HuntPfDecision.NextJoinedLatch(inParty: false, joinedLatch: true));
	}

	[Theory]
	[InlineData(100, HuntPfDecision.MinRetryIntervalMs)]
	[InlineData(3_000, 3_000)]
	[InlineData(999_999, HuntPfDecision.MaxRetryIntervalMs)]
	public void ClampRetryIntervalMs(int input, int expected)
		=> Assert.Equal(expected, HuntPfDecision.ClampRetryIntervalMs(input));

	[Theory]
	[InlineData(50, HuntPfDecision.MinOpenSettleMs)]
	[InlineData(750, 750)]
	[InlineData(9_999, HuntPfDecision.MaxOpenSettleMs)]
	public void ClampOpenSettleMs(int input, int expected)
		=> Assert.Equal(expected, HuntPfDecision.ClampOpenSettleMs(input));

	[Fact]
	public void IsActionReady_and_NextActionAt()
	{
		Assert.True(HuntPfDecision.IsActionReady(1_000, 500));
		Assert.False(HuntPfDecision.IsActionReady(500, 1_000));
		Assert.Equal(4_000, HuntPfDecision.NextActionAt(1_000, 3_000));
		Assert.Equal(1_000 + HuntPfDecision.MinRetryIntervalMs, HuntPfDecision.NextActionAt(1_000, 1));
	}
}
