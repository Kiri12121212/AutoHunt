#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;

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

	/// <summary>
	/// Distance used for NearbyARank / divert eligibility (yalms).
	/// When a conductor flag WorldPos is known, callers set this to
	/// <see cref="EngageTargetDecision.EligibilityDistance"/>; otherwise same as
	/// <see cref="Distance"/>.
	/// </summary>
	public float EligibilityDistance { get; init; }

	/// <summary>
	/// Mob → hunt position hint distance (yalms, typically XZ).
	/// <see cref="float.PositiveInfinity"/> when no hint / unused.
	/// Hint sources: conductor flag, HuntAlerts, soft Sonar chat (no Sonar IPC).
	/// </summary>
	public float DistanceToHint { get; init; }

	public bool IsAlive { get; init; }
}

/// <summary>
/// Pure engage-target priority (A-train):
/// 1) Conductor fight target,
/// 2) Party ally fight target (nearest mob),
/// 3) Nearby A-rank in scan range (prefer nearest to Sonar/HA/conductor hint when enabled),
/// else none. No player follow. S-ranks never selected via the A-rank path.
/// </summary>
public static class EngageTargetDecision
{
	/// <summary>
	/// Default scan radius for nearby A-ranks (yalms) — large enough to cover a
	/// typical conductor-flag area after unmount, not a melee bubble.
	/// </summary>
	public const float DefaultARankScanRange = 175f;

	public const float MinARankScanRange = 15f;
	public const float MaxARankScanRange = 350f;

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
	/// Scan / divert eligibility distance when an active conductor flag WorldPos is known.
	/// Uses <c>min(player→mob, flag→mob)</c> so (1) mobs near the flag are found even if
	/// the player is slightly offset after unmount/TP, and (2) mid-path divert still works
	/// when the player is already near a mob but farther from the flag. When flag distance
	/// is unknown / invalid, falls back to player→mob only.
	/// </summary>
	public static float EligibilityDistance(float playerToMob, float? flagToMob)
	{
		if (flagToMob is not { } flagDist
		    || float.IsNaN(flagDist)
		    || float.IsInfinity(flagDist)
		    || flagDist < 0f)
			return playerToMob;
		return Math.Min(playerToMob, flagDist);
	}

	/// <summary>
	/// Pick engage mob. Conductor &gt; party fight &gt; nearby A-rank.
	/// NearbyARank eligibility uses <see cref="EngageMobCandidate.EligibilityDistance"/> vs scan range.
	/// When <paramref name="preferNearHint"/> and candidates carry finite
	/// <see cref="EngageMobCandidate.DistanceToHint"/>, NearbyARank prefers
	/// the mob closest to the hunt hint (still within eligibility range).
	/// </summary>
	public static EngageTargetPick Resolve(
		IReadOnlyList<EngageMobCandidate> candidates,
		float scanRange,
		bool preferNearHint = true)
	{
		var range = ClampARankScanRange(scanRange);

		var bestConductor = -1;
		var bestConductorDist = float.PositiveInfinity;
		var bestParty = -1;
		var bestPartyDist = float.PositiveInfinity;
		var bestA = -1;
		var bestAHintDist = float.PositiveInfinity;
		var bestAEligDist = float.PositiveInfinity;
		var bestAUsedHint = false;

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

			if (!c.IsARank || c.EligibilityDistance > range)
				continue;

			var hasHint = preferNearHint
				&& !float.IsNaN(c.DistanceToHint)
				&& !float.IsInfinity(c.DistanceToHint)
				&& c.DistanceToHint >= 0f;

			if (hasHint)
			{
				// Hint mode wins over pure eligibility-distance picks.
				if (!bestAUsedHint
				    || c.DistanceToHint < bestAHintDist
				    || (c.DistanceToHint == bestAHintDist && c.EligibilityDistance < bestAEligDist))
				{
					bestA = c.Index;
					bestAHintDist = c.DistanceToHint;
					bestAEligDist = c.EligibilityDistance;
					bestAUsedHint = true;
				}
			}
			else if (!bestAUsedHint && c.EligibilityDistance < bestAEligDist)
			{
				bestA = c.Index;
				bestAEligDist = c.EligibilityDistance;
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

	/// <summary>Compact, side-effect-free target diagnostic for helper logging.</summary>
	public static string Describe(in EngageTargetPick pick)
		=> $"target={pick.Kind}, index={pick.Index}";

	/// <summary>Within engage range of the chosen mob → ready for combat phase.</summary>
	public static bool ShouldEnterCombatOnMob(float? distanceToMob, float engageRange)
		=> CombatDecision.IsWithinEngageRange(distanceToMob, engageRange);

	/// <summary>
	/// Skip conductor chat flag intake while fighting an A-rank so a map-link
	/// does not abort mid-pull. Approach / non-A combat still accept chat.
	/// Callers should <b>defer</b> (not drop) the flag and flush after combat.
	/// </summary>
	public static bool ShouldSuppressChatWhileFightingARank(
		bool inCombatPhase,
		bool targetIsARank)
		=> inCombatPhase && targetIsARank;

	/// <summary>
	/// Abort flag Navigate and divert toward this mob (scan range).
	/// Pass <see cref="EligibilityDistance"/> when a flag WorldPos is known.
	/// </summary>
	public static bool ShouldDivertFromFlagNav(float distanceToMob, float divertRange)
		=> distanceToMob >= 0f
			&& !float.IsNaN(distanceToMob)
			&& !float.IsInfinity(distanceToMob)
			&& distanceToMob <= ClampARankScanRange(divertRange);

	/// <summary>
	/// Max |player.Y − mob.Y| while still InFlight before PathStop + Unmount.
	/// Higher = still descending toward the mob floor.
	/// </summary>
	public const float EngageUnmountMaxVerticalDelta = 3f;

	/// <summary>
	/// Mob is close enough to PathStop + Unmount (within engage range, mounted/flying,
	/// and near the mob's floor altitude when still in flight).
	/// </summary>
	public static bool ShouldLandAndUnmountForEngage(
		bool mounted,
		bool inFlight,
		float distanceToMob,
		float engageRange,
		float verticalDeltaToMob = 0f)
	{
		if (!(mounted || inFlight))
			return false;
		if (!ShouldEnterCombatOnMob(distanceToMob, engageRange))
			return false;
		// Still high above the mob: keep flying down; do not dismount mid-air.
		if (inFlight
			&& verticalDeltaToMob > EngageUnmountMaxVerticalDelta)
			return false;
		return true;
	}

	/// <summary>
	/// Mounted/flying and mob in divert range → keep approaching the mob floor
	/// (caller issues Move) until <see cref="ShouldLandAndUnmountForEngage"/>.
	/// </summary>
	public static bool ShouldApproachMobFloorForEngage(
		bool mounted,
		bool inFlight,
		float distanceToMob,
		float divertRange)
		=> (mounted || inFlight)
			&& ShouldDivertFromFlagNav(distanceToMob, divertRange);

	/// <summary>
	/// Flush a combat-deferred flag once combat is idle (level-triggered).
	/// Edge-only missed clears when combat.Clear() skipped the falling edge.
	/// </summary>
	public static bool ShouldFlushDeferredFlagAfterCombat(
		bool wasInCombatPhase,
		bool inCombatPhase,
		bool hasDeferredFlag)
		=> !inCombatPhase && hasDeferredFlag;
}
