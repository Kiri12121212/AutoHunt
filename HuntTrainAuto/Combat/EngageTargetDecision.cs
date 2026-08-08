#nullable enable
using System;
using System.Collections.Generic;

namespace HuntTrainAuto.Combat;

/// <summary>How an engage target was chosen (no player-follow).</summary>
public enum EngageTargetKind
{
	None,
	/// <summary>Conductor is in combat — join their BattleNpc target.</summary>
	ConductorFight,
	/// <summary>Party ally is in combat — join their BattleNpc target.</summary>
	PartyFight,
	/// <summary>Nearest living NotoriousMonster Rank A within scan range.</summary>
	NearbyARank,
}

/// <summary>Pure engage pick result.</summary>
public readonly struct EngageTargetPick
{
	public required EngageTargetKind Kind { get; init; }

	/// <summary>Index into the candidate list; -1 when <see cref="EngageTargetKind.None"/>.</summary>
	public int Index { get; init; }

	public bool Found => Kind != EngageTargetKind.None && Index >= 0;
}

/// <summary>One BattleNpc candidate for engage decisions.</summary>
public readonly struct EngageMobCandidate
{
	public required int Index { get; init; }

	/// <summary>True when this mob is the conductor's current target and conductor is in combat.</summary>
	public bool IsConductorFightTarget { get; init; }

	/// <summary>True when this mob is a party ally's current target and that ally is in combat.</summary>
	public bool IsPartyFightTarget { get; init; }

	/// <summary>True when NameId/BaseId matches NotoriousMonster Rank A.</summary>
	public bool IsARank { get; init; }

	/// <summary>Player → mob distance (yalms).</summary>
	public float Distance { get; init; }

	public bool IsAlive { get; init; }
}

/// <summary>
/// Pure engage-target priority (A-train):
/// 1) Conductor fight target,
/// 2) Party ally fight target (nearest mob),
/// 3) Nearest A-rank in scan range,
/// else none. No player follow. S-ranks never selected via the A-rank path.
/// </summary>
public static class EngageTargetDecision
{
	/// <summary>Default scan radius for nearby A-ranks (yalms).</summary>
	public const float DefaultARankScanRange = 50f;

	public const float MinARankScanRange = 10f;
	public const float MaxARankScanRange = 100f;

	public static float ClampARankScanRange(float range)
	{
		if (float.IsNaN(range) || float.IsInfinity(range))
			return DefaultARankScanRange;
		if (range < MinARankScanRange)
			return MinARankScanRange;
		if (range > MaxARankScanRange)
			return MaxARankScanRange;
		return range;
	}

	/// <summary>
	/// Pick engage mob. Conductor &gt; party fight &gt; nearby A-rank.
	/// </summary>
	public static EngageTargetPick Resolve(
		IReadOnlyList<EngageMobCandidate> candidates,
		float scanRange)
	{
		var range = ClampARankScanRange(scanRange);

		var bestConductor = -1;
		var bestConductorDist = float.PositiveInfinity;
		var bestParty = -1;
		var bestPartyDist = float.PositiveInfinity;
		var bestA = -1;
		var bestADist = float.PositiveInfinity;

		for (var i = 0; i < candidates.Count; i++)
		{
			var c = candidates[i];
			if (!c.IsAlive)
				continue;

			if (c.IsConductorFightTarget && c.Distance < bestConductorDist)
			{
				bestConductor = c.Index;
				bestConductorDist = c.Distance;
			}

			if (c.IsPartyFightTarget && c.Distance < bestPartyDist)
			{
				bestParty = c.Index;
				bestPartyDist = c.Distance;
			}

			if (c.IsARank
				&& c.Distance <= range
				&& c.Distance < bestADist)
			{
				bestA = c.Index;
				bestADist = c.Distance;
			}
		}

		if (bestConductor >= 0)
		{
			return new EngageTargetPick
			{
				Kind = EngageTargetKind.ConductorFight,
				Index = bestConductor,
			};
		}

		if (bestParty >= 0)
		{
			return new EngageTargetPick
			{
				Kind = EngageTargetKind.PartyFight,
				Index = bestParty,
			};
		}

		if (bestA >= 0)
		{
			return new EngageTargetPick
			{
				Kind = EngageTargetKind.NearbyARank,
				Index = bestA,
			};
		}

		return new EngageTargetPick { Kind = EngageTargetKind.None, Index = -1 };
	}

	/// <summary>Within engage range of the chosen mob → ready for combat phase.</summary>
	public static bool ShouldEnterCombatOnMob(float? distanceToMob, float engageRange)
		=> CombatDecision.IsWithinEngageRange(distanceToMob, engageRange);

	/// <summary>
	/// Skip conductor chat flag intake while fighting an A-rank so a map-link
	/// does not abort mid-pull. Approach / non-A combat still accept chat.
	/// </summary>
	public static bool ShouldSuppressChatWhileFightingARank(
		bool inCombatPhase,
		bool targetIsARank)
		=> inCombatPhase && targetIsARank;
}
