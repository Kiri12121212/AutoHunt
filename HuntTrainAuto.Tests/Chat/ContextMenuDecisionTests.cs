namespace HuntTrainAuto.Tests.Chat;

public sealed class ContextMenuDecisionTests
{
	[Fact]
	public void ShouldShow_true_for_ChatLog_with_player()
	{
		Assert.True(ContextMenuDecision.ShouldShow(
			contextMenuEnabled: true,
			addonName: "ChatLog",
			hasPlayerName: true));
	}

	[Fact]
	public void ShouldShow_true_for_null_addon_world_target()
	{
		Assert.True(ContextMenuDecision.ShouldShow(
			contextMenuEnabled: true,
			addonName: null,
			hasPlayerName: true));
	}

	[Fact]
	public void ShouldShow_false_when_disabled()
	{
		Assert.False(ContextMenuDecision.ShouldShow(
			contextMenuEnabled: false,
			addonName: "ChatLog",
			hasPlayerName: true));
	}

	[Fact]
	public void ShouldShow_false_without_player_name()
	{
		Assert.False(ContextMenuDecision.ShouldShow(
			contextMenuEnabled: true,
			addonName: "ChatLog",
			hasPlayerName: false));
	}

	[Fact]
	public void ShouldShow_false_for_non_public_world_when_rejected()
	{
		Assert.False(ContextMenuDecision.ShouldShow(
			contextMenuEnabled: true,
			addonName: "ChatLog",
			hasPlayerName: true,
			rejectNonPublicWorld: true));
	}

	[Fact]
	public void ShouldShow_false_for_unknown_addon()
	{
		Assert.False(ContextMenuDecision.ShouldShow(
			contextMenuEnabled: true,
			addonName: "Inventory",
			hasPlayerName: true));
	}

	[Theory]
	[InlineData("PartyMemberList")]
	[InlineData("FriendList")]
	[InlineData("CrossWorldLinkshell")]
	[InlineData("_PartyList")]
	public void ShouldShow_true_for_hta_valid_addons(string addon)
	{
		Assert.True(ContextMenuDecision.ShouldShow(
			contextMenuEnabled: true,
			addonName: addon,
			hasPlayerName: true));
	}
}
