#nullable enable
using Xunit;

namespace HuntTrainAuto.Tests;

public sealed class ChatSuppressTests
{
	[Fact]
	public void ShouldSuppress_true_for_non_conductor_non_maplink_when_enabled()
	{
		Assert.True(ChatSuppress.ShouldSuppress(
			suppressChatOtherPlayers: true,
			isMapLink: false,
			isConductorMessage: false,
			conductorCount: 1));
	}

	[Fact]
	public void ShouldSuppress_false_for_conductor()
	{
		Assert.False(ChatSuppress.ShouldSuppress(
			suppressChatOtherPlayers: true,
			isMapLink: false,
			isConductorMessage: true,
			conductorCount: 1));
	}

	[Fact]
	public void ShouldSuppress_false_for_map_link()
	{
		Assert.False(ChatSuppress.ShouldSuppress(
			suppressChatOtherPlayers: true,
			isMapLink: true,
			isConductorMessage: false,
			conductorCount: 1));
	}

	[Fact]
	public void ShouldSuppress_false_when_flag_off()
	{
		Assert.False(ChatSuppress.ShouldSuppress(
			suppressChatOtherPlayers: false,
			isMapLink: false,
			isConductorMessage: false,
			conductorCount: 1));
	}

	[Fact]
	public void ShouldSuppress_false_when_conductors_empty()
	{
		Assert.False(ChatSuppress.ShouldSuppress(
			suppressChatOtherPlayers: true,
			isMapLink: false,
			isConductorMessage: false,
			conductorCount: 0));
	}

	[Theory]
	[InlineData(true, true, true, 2, false)]
	[InlineData(true, true, false, 2, false)]
	[InlineData(true, false, true, 2, false)]
	[InlineData(false, false, false, 2, false)]
	[InlineData(true, false, false, 0, false)]
	[InlineData(true, false, false, 3, true)]
	public void ShouldSuppress_covers_hta_gate_combinations(
		bool suppress,
		bool isMapLink,
		bool isConductor,
		int conductorCount,
		bool expected)
	{
		Assert.Equal(
			expected,
			ChatSuppress.ShouldSuppress(suppress, isMapLink, isConductor, conductorCount));
	}
}
