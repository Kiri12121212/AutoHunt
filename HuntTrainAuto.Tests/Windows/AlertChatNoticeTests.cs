#nullable enable

using HuntTrainAuto.HuntAlerts;
using HuntTrainAuto.Windows;

namespace HuntTrainAuto.Tests.Windows;

public sealed class AlertChatNoticeTests
{
	[Fact]
	public void FormatLine_train_with_world()
	{
		var msg = new HuntTrainMessage
		{
			huntType = HuntAlertsFilter.HuntTypeATrain,
			huntKind = "Dawntrail",
			huntWorld = "Shiva",
		};
		Assert.Equal(
			"Dawntrail train starting on Shiva! (Click for info)",
			AlertChatNotice.FormatLine(msg));
		Assert.Equal(AlertChatNotice.TrainUiForeground, AlertChatNotice.UiForegroundFor(msg));
	}

	[Fact]
	public void FormatLine_train_without_world()
	{
		var msg = new HuntTrainMessage
		{
			huntType = HuntAlertsFilter.HuntTypeATrain,
			huntKind = "Dawntrail",
		};
		Assert.Equal(
			"Dawntrail train starting! (Click for info)",
			AlertChatNotice.FormatLine(msg));
	}

	[Fact]
	public void FormatLine_srank_with_creature_and_instance()
	{
		var msg = new HuntTrainMessage
		{
			huntType = HuntAlertsFilter.HuntTypeSRank,
			huntKind = "Dawntrail",
			creatureName = "Ker",
			huntWorld = "Shiva",
			instance = 2,
		};
		Assert.Equal(
			"Dawntrail S Rank Ker (i2) spawned on Shiva! (Click for info)",
			AlertChatNotice.FormatLine(msg));
		Assert.Equal(AlertChatNotice.SRankUiForeground, AlertChatNotice.UiForegroundFor(msg));
	}

	[Fact]
	public void FormatLine_srank_without_creature()
	{
		var msg = new HuntTrainMessage
		{
			huntType = HuntAlertsFilter.HuntTypeSRank,
			huntKind = "Endwalker",
			huntWorld = "Zodiark",
		};
		Assert.Equal(
			"Endwalker S Rank spawned on Zodiark! (Click for info)",
			AlertChatNotice.FormatLine(msg));
	}
}
