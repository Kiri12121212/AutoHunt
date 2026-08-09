#nullable enable
using System.Collections.Generic;

namespace HuntTrainAuto.Tests.Combat;

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
		var pick = EngageTargetDecision.Resolve(candidates, 50f, preferNearHint: false);
		Assert.Equal(EngageTargetKind.NearbyARank, pick.Kind);
		Assert.Equal(1, pick.Index);
	}

	[Fact]
	public void Resolve_prefers_a_rank_near_position_hint()
	{
		// Closer to player is index 0; closer to hint is index 1.
		var candidates = new List<EngageMobCandidate>
		{
			Mob(0, isA: true, dist: 10f, hintDist: 40f),
			Mob(1, isA: true, dist: 25f, hintDist: 5f),
			Mob(2, isA: true, dist: 30f, hintDist: 50f),
		};
		var pick = EngageTargetDecision.Resolve(candidates, 50f, preferNearHint: true);
		Assert.Equal(EngageTargetKind.NearbyARank, pick.Kind);
		Assert.Equal(1, pick.Index);
	}

	[Fact]
	public void Resolve_hint_prefer_falls_back_to_player_distance_when_disabled()
	{
		var candidates = new List<EngageMobCandidate>
		{
			Mob(0, isA: true, dist: 10f, hintDist: 40f),
			Mob(1, isA: true, dist: 25f, hintDist: 5f),
		};
		var pick = EngageTargetDecision.Resolve(candidates, 50f, preferNearHint: false);
		Assert.Equal(EngageTargetKind.NearbyARank, pick.Kind);
		Assert.Equal(0, pick.Index);
	}

	[Fact]
	public void Resolve_hint_prefer_ignores_infinite_hint_distance()
	{
		var candidates = new List<EngageMobCandidate>
		{
			Mob(0, isA: true, dist: 30f, hintDist: float.PositiveInfinity),
			Mob(1, isA: true, dist: 12f, hintDist: float.PositiveInfinity),
		};
		var pick = EngageTargetDecision.Resolve(candidates, 50f, preferNearHint: true);
		Assert.Equal(EngageTargetKind.NearbyARank, pick.Kind);
		Assert.Equal(1, pick.Index);
	}

	[Fact]
	public void Resolve_hint_tie_breaks_by_player_distance()
	{
		var candidates = new List<EngageMobCandidate>
		{
			Mob(0, isA: true, dist: 30f, hintDist: 8f),
			Mob(1, isA: true, dist: 15f, hintDist: 8f),
		};
		var pick = EngageTargetDecision.Resolve(candidates, 50f, preferNearHint: true);
		Assert.Equal(EngageTargetKind.NearbyARank, pick.Kind);
		Assert.Equal(1, pick.Index);
	}

	[Fact]
	public void Resolve_conductor_still_beats_hint_near_a_rank()
	{
		var candidates = new List<EngageMobCandidate>
		{
			Mob(0, isA: true, dist: 5f, hintDist: 1f),
			Mob(1, conductor: true, dist: 40f, hintDist: 100f),
		};
		var pick = EngageTargetDecision.Resolve(candidates, 50f, preferNearHint: true);
		Assert.Equal(EngageTargetKind.ConductorFight, pick.Kind);
		Assert.Equal(1, pick.Index);
	}

	[Fact]
	public void Resolve_prefer_near_hint_ignores_out_of_scan_range()
	{
		var candidates = new List<EngageMobCandidate>
		{
			Mob(0, isA: true, dist: 8f, hintDist: 40f),
			Mob(1, isA: true, dist: 60f, hintDist: 1f), // closest to hint but out of scan
		};
		var pick = EngageTargetDecision.Resolve(candidates, 50f, preferNearHint: true);
		Assert.Equal(EngageTargetKind.NearbyARank, pick.Kind);
		Assert.Equal(0, pick.Index);
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
	public void ShouldEnterCombatOnMob_uses_configured_range_not_melee_walk_in()
	{
		// Vnav stops at EngageRange; BossMod closes further — do not require 3y.
		Assert.True(EngageTargetDecision.ShouldEnterCombatOnMob(20f, 25f));
		Assert.True(EngageTargetDecision.ShouldEnterCombatOnMob(5f, 25f));
		Assert.False(EngageTargetDecision.ShouldEnterCombatOnMob(26f, 25f));
	}

	[Theory]
	[InlineData(true, true, true)]
	[InlineData(true, false, false)]
	[InlineData(false, true, false)]
	[InlineData(false, false, false)]
	public void ShouldSuppressChatWhileFightingARank(
		bool inCombat,
		bool targetIsA,
		bool expected)
		=> Assert.Equal(
			expected,
			EngageTargetDecision.ShouldSuppressChatWhileFightingARank(inCombat, targetIsA));

	[Theory]
	[InlineData(10f, 50f, true)]
	[InlineData(50f, 50f, true)]
	[InlineData(51f, 50f, false)]
	[InlineData(-1f, 50f, false)]
	public void ShouldDivertFromFlagNav(float dist, float range, bool expected)
		=> Assert.Equal(expected, EngageTargetDecision.ShouldDivertFromFlagNav(dist, range));

	[Theory]
	[InlineData(true, false, 3f, 5f, 0f, true)]
	[InlineData(false, true, 3f, 5f, 0.5f, true)]
	[InlineData(false, true, 3f, 5f, 10f, false)] // still high above mob floor
	[InlineData(false, false, 3f, 5f, 0f, false)]
	[InlineData(true, false, 20f, 5f, 0f, false)]
	public void ShouldLandAndUnmountForEngage(
		bool mounted,
		bool inFlight,
		float dist,
		float engageRange,
		float verticalDelta,
		bool expected)
		=> Assert.Equal(
			expected,
			EngageTargetDecision.ShouldLandAndUnmountForEngage(
				mounted,
				inFlight,
				dist,
				engageRange,
				verticalDelta));

	[Theory]
	[InlineData(true, false, 10f, 50f, true)]
	[InlineData(false, true, 10f, 50f, true)]
	[InlineData(false, false, 10f, 50f, false)]
	[InlineData(true, false, 60f, 50f, false)]
	public void ShouldApproachMobFloorForEngage(
		bool mounted,
		bool inFlight,
		float dist,
		float divertRange,
		bool expected)
		=> Assert.Equal(
			expected,
			EngageTargetDecision.ShouldApproachMobFloorForEngage(
				mounted,
				inFlight,
				dist,
				divertRange));

	[Theory]
	[InlineData(true, false, true, true)]
	[InlineData(true, true, true, false)]
	[InlineData(false, false, true, true)]
	[InlineData(true, false, false, false)]
	public void ShouldFlushDeferredFlagAfterCombat(
		bool wasInCombat,
		bool inCombat,
		bool hasDeferred,
		bool expected)
		=> Assert.Equal(
			expected,
			EngageTargetDecision.ShouldFlushDeferredFlagAfterCombat(wasInCombat, inCombat, hasDeferred));

	[Fact]
	public void ARankHuntIndex_only_rank_a()
	{
		var ids = ARankHuntIndex.BuildARankIds(
		[
			(10u, 100u, (byte)HuntMarkRank.B),
			(20u, 200u, (byte)HuntMarkRank.A),
			(30u, 300u, (byte)HuntMarkRank.S),
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
		bool alive = true,
		float hintDist = float.PositiveInfinity)
		=> new()
		{
			Index = index,
			IsConductorFightTarget = conductor,
			IsPartyFightTarget = party,
			IsARank = isA,
			Distance = dist,
			DistanceToHint = hintDist,
			IsAlive = alive,
		};
}
