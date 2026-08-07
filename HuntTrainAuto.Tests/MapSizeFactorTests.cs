#nullable enable
using Xunit;

namespace HuntTrainAuto.Tests;

public sealed class MapSizeFactorTests
{
	[Fact]
	public void Prefers_mapId_over_first_territory_row()
	{
		var maps = new[]
		{
			(RowId: 1u, TerritoryTypeId: 100u, SizeFactor: 100f),
			(RowId: 2u, TerritoryTypeId: 100u, SizeFactor: 200f),
		};

		Assert.Equal(200f, MapSizeFactor.Resolve(mapId: 2, territoryTypeId: 100, maps));
	}

	[Fact]
	public void Falls_back_to_first_territory_when_mapId_zero()
	{
		var maps = new[]
		{
			(RowId: 1u, TerritoryTypeId: 100u, SizeFactor: 100f),
			(RowId: 2u, TerritoryTypeId: 100u, SizeFactor: 200f),
		};

		Assert.Equal(100f, MapSizeFactor.Resolve(mapId: 0, territoryTypeId: 100, maps));
	}

	[Fact]
	public void Falls_back_to_first_territory_when_mapId_missing()
	{
		var maps = new[]
		{
			(RowId: 1u, TerritoryTypeId: 100u, SizeFactor: 100f),
			(RowId: 2u, TerritoryTypeId: 100u, SizeFactor: 200f),
		};

		Assert.Equal(100f, MapSizeFactor.Resolve(mapId: 99, territoryTypeId: 100, maps));
	}

	[Fact]
	public void Returns_null_when_territory_absent()
	{
		var maps = new[]
		{
			(RowId: 1u, TerritoryTypeId: 50u, SizeFactor: 100f),
		};

		Assert.Null(MapSizeFactor.Resolve(mapId: 0, territoryTypeId: 100, maps));
	}

	[Fact]
	public void Uses_mapId_even_when_earlier_territory_row_differs()
	{
		var maps = new[]
		{
			(RowId: 10u, TerritoryTypeId: 100u, SizeFactor: 95f),
			(RowId: 20u, TerritoryTypeId: 200u, SizeFactor: 100f),
			(RowId: 30u, TerritoryTypeId: 100u, SizeFactor: 200f),
		};

		Assert.Equal(200f, MapSizeFactor.Resolve(mapId: 30, territoryTypeId: 100, maps));
	}
}
