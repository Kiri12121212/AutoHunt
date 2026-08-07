using System.Collections.Generic;

namespace HuntTrainAuto.Tests.Chat;

public sealed class ConductorListTests
{
	[Fact]
	public void TryAdd_trims_and_appends()
	{
		var list = new List<string>();
		Assert.True(ConductorList.TryAdd(list, "  Alice  "));
		Assert.Equal(new[] { "Alice" }, list);
	}

	[Fact]
	public void TryAdd_skips_empty_and_whitespace()
	{
		var list = new List<string>();
		Assert.False(ConductorList.TryAdd(list, ""));
		Assert.False(ConductorList.TryAdd(list, "   "));
		Assert.Empty(list);
	}

	[Fact]
	public void TryAdd_skips_duplicates_case_insensitive()
	{
		var list = new List<string> { "Bob" };
		Assert.False(ConductorList.TryAdd(list, "bob"));
		Assert.False(ConductorList.TryAdd(list, " BOB "));
		Assert.Single(list);
	}

	[Fact]
	public void TryRemoveAt_removes_valid_index()
	{
		var list = new List<string> { "a", "b", "c" };
		Assert.True(ConductorList.TryRemoveAt(list, 1));
		Assert.Equal(new[] { "a", "c" }, list);
	}

	[Fact]
	public void TryRemoveAt_rejects_out_of_range()
	{
		var list = new List<string> { "a" };
		Assert.False(ConductorList.TryRemoveAt(list, -1));
		Assert.False(ConductorList.TryRemoveAt(list, 1));
		Assert.Single(list);
	}

	[Fact]
	public void Clear_empties_list()
	{
		var list = new List<string> { "a", "b" };
		ConductorList.Clear(list);
		Assert.Empty(list);
	}
}
