#nullable enable

using HuntTrainAuto.HuntAlerts;
using HuntTrainAuto.Windows;

namespace HuntTrainAuto.Tests.Windows;

public sealed class AlertRelayTests
{
	[Fact]
	public void BuildRelayText_train()
	{
		var msg = new HuntTrainMessage
		{
			huntType = HuntAlertsFilter.HuntTypeATrain,
			huntKind = "Dawntrail",
			huntWorld = "Zodiark",
			startZone = "Heritage Found",
			startLocation = "Yyasulani Station",
		};
		Assert.Equal(
			"Dawntrail train on Zodiark! Heritage Found - Yyasulani Station",
			AlertRelay.BuildRelayText(msg));
	}

	[Fact]
	public void BuildChatCommand_truncates_and_prefixes()
	{
		var msg = new HuntTrainMessage
		{
			huntType = HuntAlertsFilter.HuntTypeATrain,
			huntKind = "Dawntrail",
			huntWorld = "Zodiark",
		};
		var line = AlertRelay.BuildChatCommand(msg, "/p");
		Assert.StartsWith("/p Dawntrail train on Zodiark!", line);
		Assert.True(line.Length <= AlertRelay.MaxChatLineLength);
	}

	[Fact]
	public void DisplayFor_defaults_unknown_to_Party()
		=> Assert.Equal("Party", AlertRelay.DisplayFor("/nope"));
}
