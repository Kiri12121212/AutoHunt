#nullable enable
using System.Collections.Generic;

namespace HuntTrainAuto.Tests.Chat;

public sealed class ChatSenderTests
{
	[Fact]
	public void TryDecode_prefers_first_non_empty_player_payload()
	{
		Assert.True(ChatSender.TryDecode(
			["  Alice  ", "Bob"],
			"ignored text",
			out var name));
		Assert.Equal("Alice", name);
	}

	[Fact]
	public void TryDecode_skips_null_and_whitespace_payloads()
	{
		Assert.True(ChatSender.TryDecode(
			[null, "  ", "Carol"],
			null,
			out var name));
		Assert.Equal("Carol", name);
	}

	[Fact]
	public void TryDecode_falls_back_to_trimmed_text()
	{
		Assert.True(ChatSender.TryDecode(
			[],
			"  Dave  ",
			out var name));
		Assert.Equal("Dave", name);
	}

	[Fact]
	public void TryDecode_strips_world_suffix_on_fallback()
	{
		Assert.True(ChatSender.TryDecode(
			[null, ""],
			"Eve@Gilgamesh",
			out var name));
		Assert.Equal("Eve", name);
	}

	[Fact]
	public void TryDecode_strips_world_and_trailing_space_on_fallback()
	{
		Assert.True(ChatSender.TryDecode(
			[],
			"Frank @Leviathan",
			out var name));
		Assert.Equal("Frank", name);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("@World")]
	[InlineData("  @World  ")]
	public void TryDecode_rejects_empty_payloads_and_bad_fallback(string? text)
	{
		Assert.False(ChatSender.TryDecode([], text, out var name));
		Assert.Null(name);
	}

	[Fact]
	public void TryDecode_does_not_use_fallback_when_payload_present()
	{
		Assert.True(ChatSender.TryDecode(["PayloadName"], "Text@World", out var name));
		Assert.Equal("PayloadName", name);
	}

	[Fact]
	public void IsConductor_matches_case_insensitive()
	{
		var list = new List<string> { "Alice", "Bob" };
		Assert.True(ChatSender.IsConductor(list, "alice"));
		Assert.True(ChatSender.IsConductor(list, "BOB"));
		Assert.False(ChatSender.IsConductor(list, "Carol"));
	}

	[Fact]
	public void IsConductor_stored_with_world_matches_decoded_bare_name()
	{
		var list = new List<string> { "Alice@Gilgamesh" };
		Assert.True(ChatSender.IsConductor(list, "Alice"));
	}

	[Fact]
	public void IsConductor_bare_stored_matches_decoded_with_world()
	{
		var list = new List<string> { "Alice" };
		Assert.True(ChatSender.IsConductor(list, "Alice@Gilgamesh"));
	}

	[Fact]
	public void IsConductor_rejects_prefix_false_matches()
	{
		var list = new List<string> { "Alice", "Bob@World" };
		Assert.False(ChatSender.IsConductor(list, "Ali"));
		Assert.False(ChatSender.IsConductor(list, "AliceX"));
		Assert.False(ChatSender.IsConductor(list, "Bo"));
		Assert.False(ChatSender.IsConductor(list, "BobX@World"));
	}

	[Fact]
	public void IsConductor_rejects_null_or_empty_name()
	{
		var list = new List<string> { "Alice" };
		Assert.False(ChatSender.IsConductor(list, ""));
		Assert.False(ChatSender.IsConductor(list, null!));
	}

	[Fact]
	public void IsConductor_empty_list_never_matches()
	{
		Assert.False(ChatSender.IsConductor([], "Alice"));
	}
}
