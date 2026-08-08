#nullable enable

using HuntTrainAuto.HuntAlerts;
using HuntTrainAuto.Windows;

namespace HuntTrainAuto.Tests.Windows;

public sealed class AlertInfoDisplayTests
{
	[Fact]
	public void FormatTitle_train_uses_kind()
	{
		var msg = new HuntTrainMessage
		{
			huntType = HuntAlertsFilter.HuntTypeATrain,
			huntKind = "Dawntrail",
			huntWorld = "Zodiark",
		};
		Assert.Equal("TRAIN", AlertInfoDisplay.FormatBadge(msg));
		Assert.Equal("Dawntrail train", AlertInfoDisplay.FormatTitle(msg));
		Assert.Equal(AlertInfoDisplay.KindTrain, AlertInfoDisplay.FormatKind(msg));
	}

	[Fact]
	public void FormatTitle_srank()
	{
		var msg = new HuntTrainMessage
		{
			huntType = HuntAlertsFilter.HuntTypeSRank,
			huntKind = "Dawntrail",
			creatureName = "Ker",
		};
		Assert.Equal("S RANK", AlertInfoDisplay.FormatBadge(msg));
		Assert.Equal("Dawntrail S Rank", AlertInfoDisplay.FormatTitle(msg));
		Assert.Equal(AlertInfoDisplay.KindSRank, AlertInfoDisplay.FormatKind(msg));
	}

	[Fact]
	public void FormatPosted_prefers_Posted_Time()
	{
		var msg = new HuntTrainMessage { Posted_Time = "11:36 PM", PostedEpoch = 1 };
		Assert.Equal("11:36 PM", AlertInfoDisplay.FormatPosted(msg));
	}

	[Fact]
	public void FormatStatusSummary_none_when_null()
		=> Assert.Equal("Last HuntAlerts: (none)", AlertInfoDisplay.FormatStatusSummary(null));

	[Fact]
	public void FormatStatusSummary_includes_world_and_zone()
	{
		var msg = new HuntTrainMessage
		{
			huntType = HuntAlertsFilter.HuntTypeATrain,
			huntKind = "Dawntrail",
			huntWorld = "Zodiark",
			startZone = "Heritage Found",
		};
		Assert.Equal(
			"Last HuntAlerts: Dawntrail train · Zodiark / Heritage Found",
			AlertInfoDisplay.FormatStatusSummary(msg));
	}
}
