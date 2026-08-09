#nullable enable

namespace HuntTrainAuto.Combat;

/// <summary>
/// Local approach/combat phase for TASKS 5.8–5.9 (orchestrator Phase 7 is separate).
/// Phase 6 observes <see cref="Combat"/> via <see cref="CombatSession.InCombatPhase"/>.
/// </summary>
public enum CombatPhase
{
	/// <summary>Not approaching a mob for engage.</summary>
	Idle,

	/// <summary>Ground-pathing toward the engage mob (post-unmount / divert).</summary>
	Following,

	/// <summary>Engaged — RSR may start (Phase 6).</summary>
	Combat,
}

/// <summary>Result of <see cref="CombatDecision.Decide"/>.</summary>
public enum CombatTransitionKind
{
	/// <summary>No phase change.</summary>
	StayFollow,

	/// <summary>Enter <see cref="CombatPhase.Combat"/> (no RSR here).</summary>
	EnterCombat,

	/// <summary>
	/// Return to <see cref="CombatPhase.Idle"/>
	/// (combat end, death, master off, abort).
	/// </summary>
	StopFollow,
}

/// <summary>
/// Pure snapshot for engage / exit decisions. Wiring fills flags; no Dalamud types.
/// </summary>
/// <remarks>
/// <para><b>Vanilla detection assumptions (no ECommons):</b></para>
/// <list type="bullet">
/// <item><description>
/// Ally / local combat = character <c>StatusFlags.InCombat</c>
/// (or local <c>ConditionFlag.InCombat</c> for the player).
/// </description></item>
/// <item><description>
/// "Hunt mob targeted by party" = party member (or local) who is <b>in combat</b>
/// and whose <c>TargetObject</c> is a living <c>ObjectKind.BattleNpc</c>.
/// Bare tab-target without engage does not count. We do <b>not</b> classify Rank A/B/S
/// without excel/hunt tables.
/// </description></item>
/// <item><description>
/// Must not EnterCombat merely from flag arrival / unmount —
/// caller only evaluates while approaching (or already in Combat).
/// EnterCombat from the mob path is owned by <see cref="EngageTargetHelper"/>.
/// </description></item>
/// </list>
/// </remarks>
public readonly struct CombatEngageSnapshot
{
	public bool PluginEnabled { get; init; }

	/// <summary>Player dead / unconscious (HP ≤ 0 or Unconscious).</summary>
	public bool PlayerDead { get; init; }

	/// <summary>A party member (or local) targets a BattleNpc.</summary>
	public bool PartyTargetsHuntMob { get; init; }

	/// <summary>Player → that targeted BattleNpc; null if none.</summary>
	public float? DistanceToPartyHuntMob { get; init; }

	/// <summary>Local player InCombat.</summary>
	public bool PlayerInCombat { get; init; }

	/// <summary>Any other party ally InCombat (StatusFlags).</summary>
	public bool AnyPartyAllyInCombat { get; init; }

	/// <summary>
	/// Mob that triggered EnterCombat is still InCombat
	/// (latched EntityId).
	/// </summary>
	public bool LatchedEngageTargetInCombat { get; init; }

	/// <summary>Clamped <see cref="Configuration.EngageRange"/>.</summary>
	public float EngageRange { get; init; }
}

/// <summary>
/// Pure combat-transition decisions (TASKS 5.8–5.9 / brief 5.4).
/// Soft-fail friendly: never throws; unknown distances simply fail engage checks.
/// </summary>
public static class CombatDecision
{
	/// <summary>Default <see cref="Configuration.EngageRange"/> (yalms).</summary>
	public const float DefaultEngageRange = 25f;

	/// <summary>Minimum configurable engage range (yalms).</summary>
	public const float MinEngageRange = 5f;

	/// <summary>Maximum configurable engage range (yalms).</summary>
	public const float MaxEngageRange = 60f;

	/// <summary>
	/// Clamp to [<see cref="MinEngageRange"/>, <see cref="MaxEngageRange"/>].
	/// NaN / Infinity → <see cref="DefaultEngageRange"/>.
	/// </summary>
	public static float ClampEngageRange(float range)
	{
		if (float.IsNaN(range) || float.IsInfinity(range))
			return DefaultEngageRange;
		if (range < MinEngageRange)
			return MinEngageRange;
		if (range > MaxEngageRange)
			return MaxEngageRange;
		return range;
	}

	/// <summary>True when a known distance is ≤ engage range.</summary>
	public static bool IsWithinEngageRange(float? distance, float engageRange)
		=> distance is float d
			&& !float.IsNaN(d)
			&& !float.IsInfinity(d)
			&& d <= engageRange;

	/// <summary>
	/// Enter combat when the player is already fighting, or a party member
	/// in combat targets a BattleNpc within engage range.
	/// Primary EnterCombat path is <see cref="EngageTargetHelper"/> (mob approach).
	/// </summary>
	public static bool ShouldEnterCombat(in CombatEngageSnapshot snap)
	{
		var range = ClampEngageRange(snap.EngageRange);

		if (snap.PlayerInCombat)
			return true;

		if (snap.PartyTargetsHuntMob
			&& IsWithinEngageRange(snap.DistanceToPartyHuntMob, range)
			&& (snap.AnyPartyAllyInCombat || snap.PlayerInCombat))
		{
			return true;
		}

		return false;
	}

	/// <summary>
	/// Combat ended when nobody relevant is fighting and no party hunt target remains.
	/// Includes latched engage mob (may be outside the party list).
	/// </summary>
	public static bool IsCombatEnded(in CombatEngageSnapshot snap)
		=> !snap.PlayerInCombat
			&& !snap.AnyPartyAllyInCombat
			&& !snap.LatchedEngageTargetInCombat
			&& !snap.PartyTargetsHuntMob;

	/// <summary>
	/// One transition decision from current phase + snapshot.
	/// Does not EnterCombat from <see cref="CombatPhase.Idle"/> (no early pull at flag).
	/// </summary>
	public static CombatTransitionKind Decide(CombatPhase phase, in CombatEngageSnapshot snap)
	{
		if (!snap.PluginEnabled)
		{
			return phase is CombatPhase.Following or CombatPhase.Combat
				? CombatTransitionKind.StopFollow
				: CombatTransitionKind.StayFollow;
		}

		if (snap.PlayerDead)
		{
			return phase is CombatPhase.Following or CombatPhase.Combat
				? CombatTransitionKind.StopFollow
				: CombatTransitionKind.StayFollow;
		}

		return phase switch
		{
			CombatPhase.Combat => IsCombatEnded(snap)
				? CombatTransitionKind.StopFollow
				: CombatTransitionKind.StayFollow,

			CombatPhase.Following => ShouldEnterCombat(snap)
				? CombatTransitionKind.EnterCombat
				: CombatTransitionKind.StayFollow,

			// Idle: never EnterCombat — require Following first (post-unmount approach).
			_ => CombatTransitionKind.StayFollow,
		};
	}

	/// <summary>Apply <paramref name="kind"/> to <paramref name="phase"/>.</summary>
	public static CombatPhase NextPhase(CombatPhase phase, CombatTransitionKind kind)
		=> kind switch
		{
			CombatTransitionKind.EnterCombat => CombatPhase.Combat,
			CombatTransitionKind.StopFollow => CombatPhase.Idle,
			CombatTransitionKind.StayFollow => phase,
			_ => phase,
		};

	public static string Describe(CombatTransitionKind kind)
		=> $"transition={kind}";

}
