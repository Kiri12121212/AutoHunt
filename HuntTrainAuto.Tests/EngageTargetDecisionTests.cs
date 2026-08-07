#nullable enable
using System.Collections.Generic;
using Xunit;

namespace HuntTrainAuto.Tests;

public sealed class EngageTargetDecisionTests
{
	[Theory]
	[InlineData(50f, 50f)]
	[InlineData(10f, EngageTargetDecision.MinARankScanRange)]
	[InlineData(100f, EngageTargetDecision.MaxARankScanRange)]
	[InlineData(5f, EngageTargetDecision.MinARankScanRange)]
	[InlineData(200f, EngageTargetDecision.MaxARankScanRange)]
	[InlineData(float.NaN, EngageTargetDecision.DefaultARankScanRange)]
	public void ClampARankScanRange(float input, float expected)
		=> Assert.Equal(expected, EngageTargetDecision.ClampARankScanRange(input));

	[Fact]
	public void Resolve_none_when_empty()
	{
		var pick = EngageTargetDecision.Resolve([], 50f);
		Assert.False(pick.Found);
		Assert.Equal(EngageTargetKind.None, pick.Kind);
	}

	[Fact]
	public void Resolve_prefers_conductor_fight_over_nearby_a_rank()
	{
		var candidates = new List<EngageMobCandidate>
		{
			Mob(0, isA: true, dist: 5f),
			Mob(1, conductor: true, dist: 40f),
		};
		var pick = EngageTargetDecision.Resolve(candidates, 50f);
		Assert.Equal(EngageTargetKind.ConductorFight, pick.Kind);
		Assert.Equal(1, pick.Index);
	}

	[Fact]
	public void Resolve_prefers_conductor_over_party_fight()
	{
		var candidates = new List<EngageMobCandidate>
		{
			Mob(0, party: true, dist: 5f),
			Mob(1, conductor: true, dist: 40f),
		};
		var pick = EngageTargetDecision.Resolve(candidates, 50f);
		Assert.Equal(EngageTargetKind.ConductorFight, pick.Kind);
		Assert.Equal(1, pick.Index);
	}

	[Fact]
	public void Resolve_party_fight_before_a_rank()
	{
		var candidates = new List<EngageMobCandidate>
		{
			Mob(0, isA: true, dist: 5f),
			Mob(1, party: true, dist: 20f),
		};
		var pick = EngageTargetDecision.Resolve(candidates, 50f);
		Assert.Equal(EngageTargetKind.PartyFight, pick.Kind);
		Assert.Equal(1, pick.Index);
	}

	[Fact]
	public void Resolve_nearest_party_fight_mob()
	{
		var candidates = new List<EngageMobCandidate>
		{
			Mob(0, party: true, dist: 30f),
			Mob(1, party: true, dist: 12f),
		};
		var pick = EngageTargetDecision.Resolve(candidates, 50f);
		Assert.Equal(EngageTargetKind.PartyFight, pick.Kind);
		Assert.Equal(1, pick.Index);
	}

	[Fact]
	public void Resolve_nearest_a_rank_in_range()
	{
		var candidates = new List<EngageMobCandidate>
		{
			Mob(0, isA: true, dist: 30f),
			Mob(1, isA: true, dist: 12f),
			Mob(2, isA: true, dist: 80f), // out of default 50
		};
		var pick = EngageTargetDecision.Resolve(candidates, 50f);
		Assert.Equal(EngageTargetKind.NearbyARank, pick.Kind);
		Assert.Equal(1, pick.Index);
	}

	[Fact]
	public void Resolve_ignores_dead()
	{
		var candidates = new List<EngageMobCandidate>
		{
			Mob(0, isA: true, dist: 5f, alive: false),
			Mob(1, conductor: true, dist: 8f, alive: false),
			Mob(2, party: true, dist: 6f, alive: false),
		};
		Assert.False(EngageTargetDecision.Resolve(candidates, 50f).Found);
	}

	[Fact]
	public void Resolve_ignores_a_rank_outside_scan_range()
	{
		var candidates = new List<EngageMobCandidate>
		{
			Mob(0, isA: true, dist: 60f),
		};
		Assert.False(EngageTargetDecision.Resolve(candidates, 50f).Found);
	}

	[Fact]
	public void ShouldEnterCombatOnMob_uses_engage_range()
	{
		Assert.True(EngageTargetDecision.ShouldEnterCombatOnMob(10f, 25f));
		Assert.False(EngageTargetDecision.ShouldEnterCombatOnMob(40f, 25f));
		Assert.False(EngageTargetDecision.ShouldEnterCombatOnMob(null, 25f));
	}

	[Fact]
	public void ARankHuntIndex_only_rank_a()
	{
		var ids = ARankHuntIndex.BuildARankIds(
		[
			(nameId: 10, baseId: 100, rank: (byte)HuntMarkRank.B),
			(nameId: 20, baseId: 200, rank: (byte)HuntMarkRank.A),
			(nameId: 30, baseId: 300, rank: (byte)HuntMarkRank.S),
		]);
		Assert.Equal(2, ids.Count);
		Assert.True(ARankHuntIndex.IsARank(ids, 20, 0));
		Assert.True(ARankHuntIndex.IsARank(ids, 0, 200));
		Assert.False(ARankHuntIndex.IsARank(ids, 10, 100));
		Assert.False(ARankHuntIndex.IsARank(ids, 30, 300));
	}

	private static EngageMobCandidate Mob(
		int index,
		bool conductor = false,
		bool party = false,
		bool isA = false,
		float dist = 0f,
		bool alive = true)
		=> new()
		{
			Index = index,
			IsConductorFightTarget = conductor,
			IsPartyFightTarget = party,
			IsARank = isA,
			Distance = dist,
			IsAlive = alive,
		};
}
