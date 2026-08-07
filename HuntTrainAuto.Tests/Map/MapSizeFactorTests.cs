#nullable enable

namespace HuntTrainAuto.Tests.Map;

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

	[Fact]
	public void ResolveParams_prefers_mapId_and_returns_offsets()
	{
		var maps = new[]
		{
			(RowId: 1u, TerritoryTypeId: 100u, SizeFactor: 100f, OffsetX: 10, OffsetY: 20),
			(RowId: 2u, TerritoryTypeId: 100u, SizeFactor: 200f, OffsetX: 30, OffsetY: 40),
		};

		var p = MapSizeFactor.ResolveParams(mapId: 2, territoryTypeId: 100, maps);
		Assert.NotNull(p);
		Assert.Equal(2u, p.Value.MapId);
		Assert.Equal(200f, p.Value.SizeFactor);
		Assert.Equal(30, p.Value.OffsetX);
		Assert.Equal(40, p.Value.OffsetY);
	}

	[Fact]
	public void ResolveParams_falls_back_to_first_territory()
	{
		var maps = new[]
		{
			(RowId: 1u, TerritoryTypeId: 100u, SizeFactor: 95f, OffsetX: 1, OffsetY: 2),
			(RowId: 2u, TerritoryTypeId: 100u, SizeFactor: 200f, OffsetX: 3, OffsetY: 4),
		};

		var p = MapSizeFactor.ResolveParams(mapId: 0, territoryTypeId: 100, maps);
		Assert.NotNull(p);
		Assert.Equal(1u, p.Value.MapId);
		Assert.Equal(95f, p.Value.SizeFactor);
		Assert.Equal(1, p.Value.OffsetX);
		Assert.Equal(2, p.Value.OffsetY);
	}
}
