#nullable enable

namespace HuntTrainAuto.Tests.Teleport;

public sealed class InstanceSwapHeuristicTests
{
	[Fact]
	public void Explicit_differing_from_current_wins()
	{
		var got = InstanceSwapHeuristic.ResolveTargetInstance(
			explicitReported: 2,
			currentInstance: 1,
			numberOfInstances: 2,
			killCountOnTerritory: 2,
			flagNearPriorKill: true);
		Assert.Equal(2, got);
	}

	[Fact]
	public void Stale_explicit_matching_current_yields_to_heuristic_after_two_kills()
	{
		Assert.Equal(
			2,
			InstanceSwapHeuristic.ResolveTargetInstance(
				explicitReported: 1,
				currentInstance: 1,
				numberOfInstances: 2,
				killCountOnTerritory: 2,
				flagNearPriorKill: false));
	}

	[Fact]
	public void Explicit_matching_current_kept_without_kill_signal()
	{
		Assert.Equal(
			1,
			InstanceSwapHeuristic.ResolveTargetInstance(
				explicitReported: 1,
				currentInstance: 1,
				numberOfInstances: 2,
				killCountOnTerritory: 0,
				flagNearPriorKill: false));
	}

	[Fact]
	public void After_swap_kills_on_prior_instance_do_not_retoggle()
	{
		// Live bug 20:46: two kills on i1, swapped to i2, next flag with explicit=2
		// must keep i2 — not Alternate back to i1.
		Assert.Equal(
			2,
			InstanceSwapHeuristic.ResolveTargetInstance(
				explicitReported: 2,
				currentInstance: 2,
				numberOfInstances: 2,
				killCountOnTerritory: 0, // kills scoped to current instance
				flagNearPriorKill: false));
	}

	[Fact]
	public void No_heuristic_when_fewer_than_two_effective_instances()
	{
		Assert.Equal(
			0,
			InstanceSwapHeuristic.ResolveTargetInstance(
				0, currentInstance: 1, numberOfInstances: 1,
				killCountOnTerritory: 2, flagNearPriorKill: false));
		Assert.Equal(
			0,
			InstanceSwapHeuristic.ResolveTargetInstance(
				0, currentInstance: 1, numberOfInstances: 0,
				killCountOnTerritory: 2, flagNearPriorKill: true));
	}

	[Fact]
	public void EffectiveInstanceCount_unsplit_stays_zero()
		=> Assert.Equal(0, InstanceSwapHeuristic.EffectiveInstanceCount(0, 0));

	[Fact]
	public void EffectiveInstanceCount_cache_miss_with_rawPublic_assumes_split()
	{
		Assert.Equal(2, InstanceSwapHeuristic.EffectiveInstanceCount(0, rawPublicInstance: 1));
		Assert.Equal(3, InstanceSwapHeuristic.EffectiveInstanceCount(0, rawPublicInstance: 3));
	}

	[Fact]
	public void EffectiveInstanceCount_prefers_lifestream_and_remembered()
	{
		Assert.Equal(2, InstanceSwapHeuristic.EffectiveInstanceCount(2, 0));
		Assert.Equal(3, InstanceSwapHeuristic.EffectiveInstanceCount(0, 0, rememberedMax: 3));
		Assert.Equal(2, InstanceSwapHeuristic.EffectiveInstanceCount(2, 1, rememberedMax: 3));
	}

	[Fact]
	public void No_heuristic_when_current_instance_unknown()
	{
		Assert.Equal(
			0,
			InstanceSwapHeuristic.ResolveTargetInstance(
				0, currentInstance: 0, numberOfInstances: 2,
				killCountOnTerritory: 2, flagNearPriorKill: false));
	}

	[Fact]
	public void No_heuristic_when_not_same_zone()
	{
		Assert.Equal(
			0,
			InstanceSwapHeuristic.ResolveTargetInstance(
				0, currentInstance: 1, numberOfInstances: 2,
				killCountOnTerritory: 2, flagNearPriorKill: false,
				sameZone: false));
	}

	[Fact]
	public void No_heuristic_before_two_kills_without_near_kill()
	{
		Assert.Equal(
			0,
			InstanceSwapHeuristic.ResolveTargetInstance(
				0, currentInstance: 1, numberOfInstances: 2,
				killCountOnTerritory: 0, flagNearPriorKill: false));
		Assert.Equal(
			0,
			InstanceSwapHeuristic.ResolveTargetInstance(
				0, currentInstance: 1, numberOfInstances: 2,
				killCountOnTerritory: 1, flagNearPriorKill: false));
	}

	[Fact]
	public void Third_marker_after_two_kills_swaps_instance()
	{
		Assert.Equal(
			2,
			InstanceSwapHeuristic.ResolveTargetInstance(
				0, currentInstance: 1, numberOfInstances: 2,
				killCountOnTerritory: 2, flagNearPriorKill: false));
		Assert.Equal(
			1,
			InstanceSwapHeuristic.ResolveTargetInstance(
				0, currentInstance: 2, numberOfInstances: 2,
				killCountOnTerritory: 2, flagNearPriorKill: false));
	}

	[Fact]
	public void Near_prior_kill_swaps_even_with_one_kill()
	{
		Assert.Equal(
			2,
			InstanceSwapHeuristic.ResolveTargetInstance(
				0, currentInstance: 1, numberOfInstances: 2,
				killCountOnTerritory: 1, flagNearPriorKill: true));
	}

	[Theory]
	[InlineData(1, 2, 2)]
	[InlineData(2, 2, 1)]
	[InlineData(1, 3, 2)]
	[InlineData(2, 3, 3)]
	[InlineData(3, 3, 1)]
	public void AlternateInstance_cycles(int current, int count, int expected)
		=> Assert.Equal(expected, InstanceSwapHeuristic.AlternateInstance(current, count));

	[Fact]
	public void AlternateInstance_returns_zero_when_not_applicable()
	{
		Assert.Equal(0, InstanceSwapHeuristic.AlternateInstance(1, 1));
		Assert.Equal(0, InstanceSwapHeuristic.AlternateInstance(0, 2));
	}

	[Fact]
	public void Explain_states_skip_and_fire_reasons()
	{
		Assert.Contains(
			"skip:instances<",
			InstanceSwapHeuristic.Explain(0, 1, 1, 2, false, true, 0));
		Assert.Contains(
			"skip:need-2-kills-or-nearKill",
			InstanceSwapHeuristic.Explain(0, 1, 2, 1, false, true, 0));
		Assert.Contains(
			"heuristic:→2",
			InstanceSwapHeuristic.Explain(0, 1, 2, 2, false, true, 2));
		Assert.StartsWith(
			"explicit:3",
			InstanceSwapHeuristic.Explain(3, 1, 2, 0, false, true, 3));
	}
}
