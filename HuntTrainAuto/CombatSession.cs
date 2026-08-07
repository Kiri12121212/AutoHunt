#nullable enable

namespace HuntTrainAuto;

/// <summary>
/// Local combat/follow phase latch (TASKS 5.8–5.9).
/// Phase 6 reads <see cref="InCombatPhase"/> / <see cref="Phase"/> — no RSR calls here.
/// </summary>
public sealed class CombatSession
{
	public CombatPhase Phase { get; private set; } = CombatPhase.Idle;

	/// <summary>
	/// EntityId of the follow target at EnterCombat (conductor may be outside party).
	/// Cleared on return to Idle. Used so combat-end does not fire the tick after Clear().
	/// </summary>
	public uint? LatchedEngageEntityId { get; private set; }

	/// <summary>True while party-engage combat phase is active (signal for Phase 6 RSR).</summary>
	public bool InCombatPhase => Phase == CombatPhase.Combat;

	/// <summary>Follow resolve/tick allowed when not in combat phase.</summary>
	public bool AllowsFollow => Phase != CombatPhase.Combat;

	public void EnterFollowing()
	{
		Phase = CombatPhase.Following;
		LatchedEngageEntityId = null;
	}

	public void EnterCombat(uint? latchedEngageEntityId = null)
	{
		Phase = CombatPhase.Combat;
		LatchedEngageEntityId = latchedEngageEntityId;
	}

	/// <summary>Return to Idle (combat end, death, new flag, territory, dispose, master off).</summary>
	public void Clear()
	{
		Phase = CombatPhase.Idle;
		LatchedEngageEntityId = null;
	}

	/// <summary>Apply a decision kind to the session phase.</summary>
	public void Apply(CombatTransitionKind kind, uint? latchedEngageEntityId = null)
	{
		if (kind == CombatTransitionKind.EnterCombat)
		{
			EnterCombat(latchedEngageEntityId);
			return;
		}

		Phase = CombatDecision.NextPhase(Phase, kind);
		if (kind == CombatTransitionKind.StopFollow)
			LatchedEngageEntityId = null;
	}
}
