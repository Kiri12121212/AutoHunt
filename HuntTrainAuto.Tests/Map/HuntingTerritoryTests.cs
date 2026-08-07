using System;

namespace HuntTrainAuto.Tests.Map;

public sealed class HuntingTerritoryTests
{
	[Fact]
	public void Open_world_intended_use_is_hunting()
	{
		Assert.True(HuntingTerritory.IsHuntingTerritory(123, _ => HuntingTerritory.OpenWorldIntendedUse));
	}

	[Theory]
	[InlineData(1024u)]
	[InlineData(682u)]
	[InlineData(739u)]
	[InlineData(759u)]
	[InlineData(635u)]
	[InlineData(659u)]
	public void Special_territory_ids_are_hunting(uint id)
	{
		Assert.True(HuntingTerritory.IsHuntingTerritory(id, _ => null));
	}

	[Fact]
	public void Idyllshire_is_hunting()
	{
		Assert.True(HuntingTerritory.IsHuntingTerritory(HuntingTerritory.Idyllshire, _ => null));
	}

	[Theory]
	[InlineData(0u)]
	[InlineData(128u)]
	[InlineData(9999u)]
	public void Non_hunt_territory_is_false(uint id)
	{
		Assert.False(HuntingTerritory.IsHuntingTerritory(id, _ => 0));
	}

	[Fact]
	public void Null_intended_use_fails_open_as_hunting()
	{
		Assert.True(HuntingTerritory.IsHuntingTerritory(128, _ => null));
	}

	[Fact]
	public void Null_lookup_throws()
	{
		Assert.Throws<ArgumentNullException>(() =>
			HuntingTerritory.IsHuntingTerritory(1, null!));
	}
}
