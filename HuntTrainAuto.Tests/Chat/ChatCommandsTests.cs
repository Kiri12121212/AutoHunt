using System.Collections.Generic;

namespace HuntTrainAuto.Tests.Chat;

public sealed class ChatCommandsTests
{
	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void Empty_or_whitespace_toggles_ui(string args)
	{
		var list = new List<string> { "Alice" };
		var toggled = 0;
		var opened = 0;
		var saved = 0;

		ChatCommands.Handle(args, list, () => toggled++, () => opened++, () => saved++);

		Assert.Equal(1, toggled);
		Assert.Equal(0, opened);
		Assert.Equal(0, saved);
		Assert.Equal(new[] { "Alice" }, list);
	}

	[Theory]
	[InlineData("clear")]
	[InlineData("CLEAR")]
	[InlineData(" Clear ")]
	public void Clear_empties_list_and_saves(string args)
	{
		var list = new List<string> { "Alice", "Bob" };
		var toggled = 0;
		var opened = 0;
		var saved = 0;

		ChatCommands.Handle(args, list, () => toggled++, () => opened++, () => saved++);

		Assert.Empty(list);
		Assert.Equal(1, saved);
		Assert.Equal(0, toggled);
		Assert.Equal(0, opened);
	}

	[Fact]
	public void Clear_prefix_does_not_clear()
	{
		var list = new List<string> { "Alice" };
		var opened = 0;
		var saved = 0;

		ChatCommands.Handle("clearfoo", list, () => { }, () => opened++, () => saved++);

		Assert.Equal(new[] { "Alice", "clearfoo" }, list);
		Assert.Equal(1, opened);
		Assert.Equal(1, saved);
	}

	[Theory]
	[InlineData("add Carol", "Carol")]
	[InlineData("ADD  Carol  ", "Carol")]
	[InlineData("Dave", "Dave")]
	[InlineData("  Eve  ", "Eve")]
	public void Add_or_bare_name_appends_saves_and_opens(string args, string expected)
	{
		var list = new List<string>();
		var toggled = 0;
		var opened = 0;
		var saved = 0;

		ChatCommands.Handle(args, list, () => toggled++, () => opened++, () => saved++);

		Assert.Equal(new[] { expected }, list);
		Assert.Equal(1, saved);
		Assert.Equal(1, opened);
		Assert.Equal(0, toggled);
	}

	[Fact]
	public void Duplicate_still_opens_ui_and_saves()
	{
		var list = new List<string> { "Alice" };
		var opened = 0;
		var saved = 0;

		ChatCommands.Handle("add alice", list, () => { }, () => opened++, () => saved++);

		Assert.Single(list);
		Assert.Equal(1, opened);
		Assert.Equal(1, saved);
	}

	[Fact]
	public void Bare_add_without_space_adds_name_add()
	{
		// "add   " trims to "add" before prefix check — does not match "add ".
		var list = new List<string>();
		var opened = 0;
		var saved = 0;

		ChatCommands.Handle("add   ", list, () => { }, () => opened++, () => saved++);

		Assert.Equal(new[] { "add" }, list);
		Assert.Equal(1, opened);
		Assert.Equal(1, saved);
	}
}
