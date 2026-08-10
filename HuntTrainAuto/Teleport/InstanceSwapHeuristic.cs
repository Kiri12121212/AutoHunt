#nullable enable

namespace HuntTrainAuto.Teleport;

/// <summary>
/// Infer target instance when conductor/HA omit it (kata 6cdc).
/// Explicit instance that differs from current always wins.
/// Stale explicit matching current yields to kill-based heuristics.
/// Multi-instance gate: Lifestream count ≥ 2, or count 0 (cache miss) with a split
/// signal (raw public instance &gt; 0 / remembered max). Count 0 + raw 0 = unsplit → skip.
/// </summary>
public static class InstanceSwapHeuristic
{
	/// <summary>
	/// Resolve how many instances to assume for swap math.
	/// <paramref name="lifestreamCount"/> 0 = unknown/unsplit; ≥2 = known split;
	/// 1 = single-instance report.
	/// <paramref name="rawPublicInstance"/> PublicInstances id (0 = unsplit / unknown).
	/// <paramref name="rememberedMax"/> prior max seen on this territory this session.
	/// </summary>
	public static int EffectiveInstanceCount(
		int lifestreamCount,
		int rawPublicInstance,
		int rememberedMax = 0)
	{
		if (lifestreamCount >= 2)
			return lifestreamCount;

		var remembered = rememberedMax >= 2 ? rememberedMax : 0;
		if (remembered >= 2)
			return remembered;

		// Cache miss on a split zone: public instance id is numbered (1..N).
		if (lifestreamCount == 0 && rawPublicInstance > 0)
			return rawPublicInstance > 1 ? rawPublicInstance : 2;

		return 0;
	}

	/// <summary>
	/// Resolve target instance for a same-zone flag.
	/// </summary>
	/// <param name="explicitReported">Conductor/HA instance (0 = none).</param>
	/// <param name="currentInstance">Player instance (0 = unknown → no heuristic).</param>
	/// <param name="numberOfInstances">
	/// Effective instance count from <see cref="EffectiveInstanceCount"/> (need ≥ 2).
	/// </param>
	/// <param name="killCountOnTerritory">Completed A kills this session on flag territory.</param>
	/// <param name="flagNearPriorKill">Incoming flag near a prior kill on that territory.</param>
	/// <param name="sameZone">Flag territory equals player territory.</param>
	public static int ResolveTargetInstance(
		int explicitReported,
		int currentInstance,
		int numberOfInstances,
		int killCountOnTerritory,
		bool flagNearPriorKill,
		bool sameZone = true)
	{
		// Conductor directing a real change (glyph differs from where we are).
		if (explicitReported > 0
		    && currentInstance > 0
		    && explicitReported != currentInstance)
			return explicitReported;

		if (!sameZone || numberOfInstances < 2 || currentInstance <= 0)
			return 0;

		// 3rd marker same map (2 A-ranks/map) or re-flag of an already-hunted A.
		// Stale explicit == current does not block.
		if (killCountOnTerritory < 2 && !flagNearPriorKill)
			return explicitReported > 0 ? explicitReported : 0;

		return AlternateInstance(currentInstance, numberOfInstances);
	}

	/// <summary>
	/// Next instance after <paramref name="currentInstance"/> (1-based), wrapping at
	/// <paramref name="numberOfInstances"/>. Returns 0 when swap is impossible.
	/// </summary>
	public static int AlternateInstance(int currentInstance, int numberOfInstances)
	{
		if (numberOfInstances < 2 || currentInstance <= 0)
			return 0;

		var cur = currentInstance > numberOfInstances
			? ((currentInstance - 1) % numberOfInstances) + 1
			: currentInstance;

		return cur >= numberOfInstances ? 1 : cur + 1;
	}

	/// <summary>
	/// One-line reason for live logs (fires and skips). Always includes inputs.
	/// </summary>
	public static string Explain(
		int explicitReported,
		int currentInstance,
		int numberOfInstances,
		int killCountOnTerritory,
		bool flagNearPriorKill,
		bool sameZone,
		int resolved)
	{
		var inputs =
			$"current={currentInstance} instances={numberOfInstances} "
			+ $"kills={killCountOnTerritory} nearKill={flagNearPriorKill} sameZone={sameZone} "
			+ $"explicit={explicitReported}";

		if (explicitReported > 0
		    && currentInstance > 0
		    && explicitReported != currentInstance
		    && resolved == explicitReported)
			return $"explicit:{explicitReported} {inputs}";

		if (!sameZone)
			return $"skip:not-same-zone {inputs}";
		if (numberOfInstances < 2)
			return $"skip:instances<{2} {inputs}";
		if (currentInstance <= 0)
			return $"skip:current-unknown {inputs}";
		if (killCountOnTerritory < 2 && !flagNearPriorKill)
			return explicitReported > 0
				? $"explicit-stale-or-keep:{explicitReported} {inputs}"
				: $"skip:need-2-kills-or-nearKill {inputs}";
		if (resolved > 0)
			return $"heuristic:→{resolved} {inputs}";
		return $"skip:alternate-failed {inputs}";
	}

	public static string Describe(
		int explicitReported,
		int inferred,
		int killCountOnTerritory,
		bool flagNearPriorKill,
		int numberOfInstances)
		=> explicitReported > 0 && inferred == explicitReported
			? $"instance=explicit:{explicitReported}"
			: inferred > 0
				? $"instance=heuristic:{inferred} kills={killCountOnTerritory} nearKill={flagNearPriorKill} instances={numberOfInstances}"
				: $"instance=none kills={killCountOnTerritory} nearKill={flagNearPriorKill} instances={numberOfInstances}";
}
