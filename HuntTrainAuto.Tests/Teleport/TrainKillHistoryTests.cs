#nullable enable

namespace HuntTrainAuto.Tests.Teleport;

public sealed class TrainKillHistoryTests
{
	[Fact]
	public void Record_counts_per_territory()
	{
		var h = new TrainKillHistory();
		h.Record(813, 100_000, 200_000, instanceAtKill: 1);
		h.Record(813, 300_000, 400_000, instanceAtKill: 1);
		h.Record(956, 100_000, 200_000, instanceAtKill: 2);

		Assert.Equal(2, h.CountForTerritory(813));
		Assert.Equal(1, h.CountForTerritory(956));
		Assert.Equal(0, h.CountForTerritory(1));
		Assert.Equal(3, h.TotalCount);
	}

	[Fact]
	public void CountForTerritoryOnInstance_ignores_other_instance_kills()
	{
		var h = new TrainKillHistory();
		h.Record(813, 100_000, 200_000, instanceAtKill: 1);
		h.Record(813, 300_000, 400_000, instanceAtKill: 1);
		h.Record(813, 500_000, 600_000, instanceAtKill: 2);

		Assert.Equal(2, h.CountForTerritoryOnInstance(813, 1));
		Assert.Equal(1, h.CountForTerritoryOnInstance(813, 2));
		Assert.Equal(0, h.CountForTerritoryOnInstance(813, 3));
	}

	[Fact]
	public void IsNearPriorKill_uses_map_dupe_threshold()
	{
		var h = new TrainKillHistory();
		h.Record(813, 100_000, 200_000);

		Assert.True(h.IsNearPriorKill(813, 100_000, 200_000));
		Assert.True(h.IsNearPriorKill(813, 105_000, 200_000)); // 5 map units
		Assert.False(h.IsNearPriorKill(813, 120_000, 200_000)); // 20 map units
		Assert.False(h.IsNearPriorKill(956, 100_000, 200_000));
	}

	[Fact]
	public void IsNearPriorKillOnInstance_ignores_other_instance()
	{
		var h = new TrainKillHistory();
		h.Record(813, 100_000, 200_000, instanceAtKill: 1);

		Assert.True(h.IsNearPriorKillOnInstance(813, 100_000, 200_000, currentInstance: 1));
		Assert.False(h.IsNearPriorKillOnInstance(813, 100_000, 200_000, currentInstance: 2));
	}

	[Fact]
	public void Clear_resets_all()
	{
		var h = new TrainKillHistory();
		h.Record(813, 1, 2);
		h.NoteInstanceCount(813, 2);
		h.Clear();
		Assert.Equal(0, h.TotalCount);
		Assert.Equal(0, h.CountForTerritory(813));
		Assert.Equal(0, h.RememberedMaxInstances(813));
		Assert.False(h.IsNearPriorKill(813, 1, 2));
	}

	[Fact]
	public void NoteInstanceCount_remembers_max_at_least_two()
	{
		var h = new TrainKillHistory();
		h.NoteInstanceCount(813, 1);
		Assert.Equal(0, h.RememberedMaxInstances(813));
		h.NoteInstanceCount(813, 2);
		h.NoteInstanceCount(813, 3);
		Assert.Equal(3, h.RememberedMaxInstances(813));
	}

	[Fact]
	public void SuggestsInstanceSwapReflag_scoped_to_current_instance()
	{
		var h = new TrainKillHistory();
		h.Record(813, 100_000, 200_000, instanceAtKill: 1);
		h.Record(813, 300_000, 400_000, instanceAtKill: 1);

		Assert.True(h.SuggestsInstanceSwapReflag(813, 500_000, 600_000, currentInstance: 1));
		Assert.False(h.SuggestsInstanceSwapReflag(813, 500_000, 600_000, currentInstance: 2));
		Assert.True(h.SuggestsInstanceSwapReflag(813, 100_000, 200_000, currentInstance: 1));
		Assert.False(h.SuggestsInstanceSwapReflag(813, 100_000, 200_000, currentInstance: 2));
	}

	[Fact]
	public void DescribeTerritory_includes_counts()
	{
		var h = new TrainKillHistory();
		h.Record(813, 1, 2, instanceAtKill: 1);
		Assert.Equal(
			"kills=1/1 territory=813 rememberedInstances=0 killsOnCurrent=1",
			h.DescribeTerritory(813, currentInstance: 1));
	}
}
