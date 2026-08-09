#nullable enable

namespace HuntTrainAuto.Tests.Combat;

public sealed class CombatDecisionTests
{
	[Theory]
	[InlineData(25f, 25f)]
	[InlineData(5f, CombatDecision.MinEngageRange)]
	[InlineData(60f, CombatDecision.MaxEngageRange)]
	[InlineData(4f, CombatDecision.MinEngageRange)]
	[InlineData(100f, CombatDecision.MaxEngageRange)]
	[InlineData(float.NaN, CombatDecision.DefaultEngageRange)]
	[InlineData(float.PositiveInfinity, CombatDecision.DefaultEngageRange)]
	[InlineData(float.NegativeInfinity, CombatDecision.DefaultEngageRange)]
	public void ClampEngageRange(float input, float expected)
		=> Assert.Equal(expected, CombatDecision.ClampEngageRange(input));

	[Fact]
	public void EngageRange_bounds_are_documented()
	{
		Assert.Equal(25f, CombatDecision.DefaultEngageRange);
		Assert.Equal(5f, CombatDecision.MinEngageRange);
		Assert.Equal(60f, CombatDecision.MaxEngageRange);
		Assert.True(CombatDecision.MinEngageRange > 0f);
		Assert.True(CombatDecision.DefaultEngageRange >= CombatDecision.MinEngageRange);
		Assert.True(CombatDecision.DefaultEngageRange <= CombatDecision.MaxEngageRange);
	}

	[Theory]
	[InlineData(10f, 25f, true)]
	[InlineData(25f, 25f, true)]
	[InlineData(25.1f, 25f, false)]
	[InlineData(null, 25f, false)]
	public void IsWithinEngageRange(float? distance, float range, bool expected)
		=> Assert.Equal(expected, CombatDecision.IsWithinEngageRange(distance, range));

	[Fact]
	public void ShouldEnterCombat_false_when_only_arrived_no_engage()
	{
		// Flag arrival / unmount / approach idle — no combat signals.
		var snap = Snap();
		Assert.False(CombatDecision.ShouldEnterCombat(snap));
	}

	[Fact]
	public void ShouldEnterCombat_false_when_party_targets_mob_without_engage()
	{
		// Tab-target alone is not enough (security: verified engage required).
		var snap = Snap(
			partyTargetsHuntMob: true,
			distanceToPartyMob: 12f,
			engageRange: 25f);
		Assert.False(CombatDecision.ShouldEnterCombat(snap));
	}

	[Fact]
	public void ShouldEnterCombat_when_party_targets_hunt_mob_in_range_with_ally_combat()
	{
		var snap = Snap(
			partyTargetsHuntMob: true,
			distanceToPartyMob: 12f,
			anyAllyInCombat: true,
			engageRange: 25f);
		Assert.True(CombatDecision.ShouldEnterCombat(snap));
	}

	[Fact]
	public void ShouldEnterCombat_false_when_party_targets_mob_out_of_range()
	{
		var snap = Snap(
			partyTargetsHuntMob: true,
			distanceToPartyMob: 50f,
			anyAllyInCombat: true,
			engageRange: 25f);
		Assert.False(CombatDecision.ShouldEnterCombat(snap));
	}

	[Fact]
	public void ShouldEnterCombat_when_player_already_in_combat()
	{
		var snap = Snap(playerInCombat: true);
		Assert.True(CombatDecision.ShouldEnterCombat(snap));
	}

	[Fact]
	public void IsCombatEnded_when_all_clear()
	{
		Assert.True(CombatDecision.IsCombatEnded(Snap()));
	}

	[Fact]
	public void IsCombatEnded_false_while_player_or_latched_fighting()
	{
		Assert.False(CombatDecision.IsCombatEnded(Snap(playerInCombat: true)));
		Assert.False(CombatDecision.IsCombatEnded(Snap(latchedEngageInCombat: true)));
	}

	[Fact]
	public void Decide_Combat_StopFollow_when_condition_combat_clear()
	{
		// PlayerInCombat mirrors ConditionFlag only — StatusFlags linger must not hold Combat.
		var snap = Snap(playerInCombat: false, latchedEngageInCombat: false);
		Assert.Equal(
			CombatTransitionKind.StopFollow,
			CombatDecision.Decide(CombatPhase.Combat, snap));
	}

	[Fact]
	public void IsCombatEnded_false_while_latched_engage_mob_fighting()
	{
		Assert.False(CombatDecision.IsCombatEnded(Snap(latchedEngageInCombat: true)));
	}

	[Fact]
	public void Decide_Combat_stays_while_latched_mob_fighting()
	{
		// After EnterCombat, latch keeps phase until the mob drops combat.
		var snap = Snap(latchedEngageInCombat: true);
		Assert.Equal(
			CombatTransitionKind.StayFollow,
			CombatDecision.Decide(CombatPhase.Combat, snap));
	}

	[Fact]
	public void CombatSession_latches_engage_entity_on_enter()
	{
		var session = new CombatSession();
		session.EnterFollowing();
		session.Apply(CombatTransitionKind.EnterCombat, latchedEngageEntityId: 42u);
		Assert.True(session.InCombatPhase);
		Assert.Equal(42u, session.LatchedEngageEntityId);

		session.Apply(CombatTransitionKind.StopFollow);
		Assert.Null(session.LatchedEngageEntityId);
	}

	[Fact]
	public void Decide_Idle_never_enters_combat()
	{
		var snap = Snap(
			playerInCombat: true,
			partyTargetsHuntMob: true,
			distanceToPartyMob: 5f,
			anyAllyInCombat: true);
		Assert.Equal(
			CombatTransitionKind.StayFollow,
			CombatDecision.Decide(CombatPhase.Idle, snap));
	}

	[Fact]
	public void Decide_Following_enters_combat_on_engage()
	{
		var snap = Snap(
			partyTargetsHuntMob: true,
			distanceToPartyMob: 8f,
			anyAllyInCombat: true);
		Assert.Equal(
			CombatTransitionKind.EnterCombat,
			CombatDecision.Decide(CombatPhase.Following, snap));
	}

	[Fact]
	public void Decide_Following_stays_when_no_engage()
	{
		var snap = Snap();
		Assert.Equal(
			CombatTransitionKind.StayFollow,
			CombatDecision.Decide(CombatPhase.Following, snap));
	}

	[Fact]
	public void Decide_Combat_stops_when_ended()
	{
		Assert.Equal(
			CombatTransitionKind.StopFollow,
			CombatDecision.Decide(CombatPhase.Combat, Snap()));
	}

	[Fact]
	public void Decide_Combat_stays_while_engaged()
	{
		var snap = Snap(playerInCombat: true);
		Assert.Equal(
			CombatTransitionKind.StayFollow,
			CombatDecision.Decide(CombatPhase.Combat, snap));
	}

	[Fact]
	public void Decide_death_stops_approach_or_combat()
	{
		var dead = Snap(playerDead: true);
		Assert.Equal(
			CombatTransitionKind.StopFollow,
			CombatDecision.Decide(CombatPhase.Following, dead));
		Assert.Equal(
			CombatTransitionKind.StopFollow,
			CombatDecision.Decide(CombatPhase.Combat, dead));
	}

	[Fact]
	public void Decide_plugin_off_stops_active_phases()
	{
		var off = Snap(pluginEnabled: false);
		Assert.Equal(
			CombatTransitionKind.StopFollow,
			CombatDecision.Decide(CombatPhase.Following, off));
		Assert.Equal(
			CombatTransitionKind.StopFollow,
			CombatDecision.Decide(CombatPhase.Combat, off));
	}

	[Fact]
	public void NextPhase_mapping()
	{
		Assert.Equal(
			CombatPhase.Combat,
			CombatDecision.NextPhase(CombatPhase.Following, CombatTransitionKind.EnterCombat));
		Assert.Equal(
			CombatPhase.Idle,
			CombatDecision.NextPhase(CombatPhase.Combat, CombatTransitionKind.StopFollow));
		Assert.Equal(
			CombatPhase.Following,
			CombatDecision.NextPhase(CombatPhase.Following, CombatTransitionKind.StayFollow));
	}

	[Fact]
	public void Describe_formats_transition()
		=> Assert.Equal("transition=EnterCombat", CombatDecision.Describe(CombatTransitionKind.EnterCombat));

	[Fact]
	public void CombatSession_phase_signal_for_phase6()
	{
		var session = new CombatSession();
		Assert.False(session.InCombatPhase);

		session.EnterFollowing();
		Assert.Equal(CombatPhase.Following, session.Phase);

		session.Apply(CombatTransitionKind.EnterCombat);
		Assert.True(session.InCombatPhase);

		session.Apply(CombatTransitionKind.StopFollow);
		Assert.Equal(CombatPhase.Idle, session.Phase);
		Assert.False(session.InCombatPhase);
	}

	[Fact]
	public void IsCombatEnded_false_when_hold_combat_phase()
	{
		var snap = Snap(holdCombatPhase: true);
		Assert.False(CombatDecision.IsCombatEnded(snap));
		Assert.Equal(
			CombatTransitionKind.StayFollow,
			CombatDecision.Decide(CombatPhase.Combat, snap));
	}

	[Fact]
	public void IsCombatEnded_true_without_hold_when_no_combat_signals()
	{
		var snap = Snap();
		Assert.True(CombatDecision.IsCombatEnded(snap));
		Assert.Equal(
			CombatTransitionKind.StopFollow,
			CombatDecision.Decide(CombatPhase.Combat, snap));
	}

	private static CombatEngageSnapshot Snap(
		bool pluginEnabled = true,
		bool playerDead = false,
		bool partyTargetsHuntMob = false,
		float? distanceToPartyMob = null,
		bool playerInCombat = false,
		bool anyAllyInCombat = false,
		bool latchedEngageInCombat = false,
		bool holdCombatPhase = false,
		float engageRange = CombatDecision.DefaultEngageRange)
		=> new()
		{
			PluginEnabled = pluginEnabled,
			PlayerDead = playerDead,
			PartyTargetsHuntMob = partyTargetsHuntMob,
			DistanceToPartyHuntMob = distanceToPartyMob,
			PlayerInCombat = playerInCombat,
			AnyPartyAllyInCombat = anyAllyInCombat,
			LatchedEngageTargetInCombat = latchedEngageInCombat,
			HoldCombatPhase = holdCombatPhase,
			EngageRange = engageRange,
		};
}
