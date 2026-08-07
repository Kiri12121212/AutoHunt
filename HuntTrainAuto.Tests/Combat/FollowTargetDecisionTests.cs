#nullable enable
using System.Collections.Generic;

namespace HuntTrainAuto.Tests.Combat;

public sealed class FollowTargetDecisionTests
{
	[Fact]
	public void Resolve_empty_candidates_is_none()
	{
		var pick = FollowTargetDecision.Resolve([], followConductorFirst: true);
		Assert.False(pick.Found);
		Assert.Equal(FollowTargetKind.None, pick.Kind);
		Assert.Equal(-1, pick.Index);
	}

	[Fact]
	public void Resolve_prefers_nearest_conductor_when_enabled()
	{
		var candidates = new List<FollowTargetCandidate>
		{
			C(0, "FarCond", isConductor: true, distance: 20f),
			C(1, "NearCond", isConductor: true, distance: 5f),
			C(2, "Leader", isLeader: true, distance: 1f),
		};

		var pick = FollowTargetDecision.Resolve(candidates, followConductorFirst: true);
		Assert.True(pick.Found);
		Assert.Equal(FollowTargetKind.Conductor, pick.Kind);
		Assert.Equal(1, pick.Index);
	}

	[Fact]
	public void Resolve_skips_conductor_when_FollowConductorFirst_false()
	{
		var candidates = new List<FollowTargetCandidate>
		{
			C(0, "Cond", isConductor: true, distance: 2f),
			C(1, "Leader", isLeader: true, distance: 8f),
		};

		var pick = FollowTargetDecision.Resolve(candidates, followConductorFirst: false);
		Assert.Equal(FollowTargetKind.PartyLeader, pick.Kind);
		Assert.Equal(1, pick.Index);
	}

	[Fact]
	public void Resolve_falls_back_to_leader_when_no_conductor()
	{
		var candidates = new List<FollowTargetCandidate>
		{
			C(0, "Alice", distance: 3f),
			C(1, "Leader", isLeader: true, distance: 10f),
		};

		var pick = FollowTargetDecision.Resolve(candidates, followConductorFirst: true);
		Assert.Equal(FollowTargetKind.PartyLeader, pick.Kind);
		Assert.Equal(1, pick.Index);
	}

	[Fact]
	public void Resolve_falls_back_to_nearest_in_combat_ally()
	{
		var candidates = new List<FollowTargetCandidate>
		{
			C(0, "FarFight", inCombat: true, distance: 30f),
			C(1, "NearFight", inCombat: true, distance: 4f),
			C(2, "Idle", distance: 1f),
		};

		var pick = FollowTargetDecision.Resolve(candidates, followConductorFirst: true);
		Assert.Equal(FollowTargetKind.InCombatAlly, pick.Kind);
		Assert.Equal(1, pick.Index);
	}

	[Fact]
	public void Resolve_empty_conductors_list_still_uses_leader()
	{
		// Callers mark IsConductor=false when list empty — same outcome.
		var candidates = new List<FollowTargetCandidate>
		{
			C(0, "Bob", isLeader: true, distance: 2f),
		};

		var pick = FollowTargetDecision.Resolve(candidates, followConductorFirst: true);
		Assert.Equal(FollowTargetKind.PartyLeader, pick.Kind);
		Assert.Equal(0, pick.Index);
	}

	[Fact]
	public void Resolve_no_party_no_combat_is_none()
	{
		var candidates = new List<FollowTargetCandidate>
		{
			C(0, "Stranger", distance: 2f),
		};

		var pick = FollowTargetDecision.Resolve(candidates, followConductorFirst: true);
		Assert.False(pick.Found);
		Assert.Equal(FollowTargetKind.None, pick.Kind);
	}

	[Fact]
	public void Resolve_all_out_of_combat_without_leader_is_none()
	{
		var candidates = new List<FollowTargetCandidate>
		{
			C(0, "AllyA", inCombat: false, distance: 2f),
			C(1, "AllyB", inCombat: false, distance: 3f),
		};

		var pick = FollowTargetDecision.Resolve(candidates, followConductorFirst: false);
		Assert.False(pick.Found);
	}

	[Fact]
	public void Resolve_excludes_local_player_even_if_flags_set()
	{
		var candidates = new List<FollowTargetCandidate>
		{
			C(0, "Me", isConductor: true, isLeader: true, inCombat: true, distance: 0f, isLocal: true),
			C(1, "Other", isLeader: true, distance: 5f),
		};

		var pick = FollowTargetDecision.Resolve(candidates, followConductorFirst: true);
		Assert.Equal(FollowTargetKind.PartyLeader, pick.Kind);
		Assert.Equal(1, pick.Index);
	}

	[Fact]
	public void Resolve_conductor_beats_leader_and_combat()
	{
		var candidates = new List<FollowTargetCandidate>
		{
			C(0, "Cond", isConductor: true, distance: 15f),
			C(1, "Leader", isLeader: true, distance: 1f),
			C(2, "Fighter", inCombat: true, distance: 2f),
		};

		var pick = FollowTargetDecision.Resolve(candidates, followConductorFirst: true);
		Assert.Equal(FollowTargetKind.Conductor, pick.Kind);
		Assert.Equal(0, pick.Index);
	}

	[Fact]
	public void Resolve_leader_beats_in_combat()
	{
		var candidates = new List<FollowTargetCandidate>
		{
			C(0, "Fighter", inCombat: true, distance: 1f),
			C(1, "Leader", isLeader: true, distance: 20f),
		};

		var pick = FollowTargetDecision.Resolve(candidates, followConductorFirst: true);
		Assert.Equal(FollowTargetKind.PartyLeader, pick.Kind);
		Assert.Equal(1, pick.Index);
	}

	[Fact]
	public void Resolve_uses_candidate_Index_not_list_position()
	{
		var candidates = new List<FollowTargetCandidate>
		{
			C(10, "Cond", isConductor: true, distance: 3f),
		};

		var pick = FollowTargetDecision.Resolve(candidates, followConductorFirst: true);
		Assert.Equal(10, pick.Index);
	}

	[Fact]
	public void None_is_soft_fail()
	{
		var none = FollowTargetDecision.None();
		Assert.False(none.Found);
		Assert.Equal(FollowTargetKind.None, none.Kind);
		Assert.Equal(-1, none.Index);
	}

	private static FollowTargetCandidate C(
		int index,
		string name,
		bool isConductor = false,
		bool isLeader = false,
		bool inCombat = false,
		float distance = 0f,
		bool isLocal = false)
		=> new()
		{
			Index = index,
			Name = name,
			IsConductor = isConductor,
			IsLeader = isLeader,
			InCombat = inCombat,
			Distance = distance,
			IsLocalPlayer = isLocal,
		};
}
