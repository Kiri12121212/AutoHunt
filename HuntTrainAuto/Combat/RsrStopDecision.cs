#nullable enable

namespace HuntTrainAuto.Combat;

/// <summary>
/// Events that must stop RSR (TASKS 6.5 / 6.7).
/// Plugin wiring: ImmediateClear sites call <see cref="RsrEnableHelper.Clear"/>;
/// CombatPhaseTick sites rely on combat phase exit → <see cref="RsrEnableHelper.Tick"/>.
/// </summary>
public enum RsrStopTrigger
{
	None = 0,

	/// <summary>Combat phase falling edge — mob dead / party combat ended.</summary>
	CombatPhaseExit,

	/// <summary>Local player dead / unconscious.</summary>
	Death,

	/// <summary>New conductor hunt flag (pipeline abort).</summary>
	FlagChange,

	/// <summary>Left hunting territory.</summary>
	TerritoryLeave,

	/// <summary><c>Config.Enabled</c> turned off (Framework returns before Tick).</summary>
	MasterOff,

	/// <summary>Plugin dispose.</summary>
	Dispose,
}

/// <summary>How Plugin should drive <c>RotationStop</c> for a trigger.</summary>
public enum RsrStopPath
{
	/// <summary>No stop for this trigger.</summary>
	None,

	/// <summary>
	/// Call <see cref="RsrEnableHelper.Clear"/> immediately.
	/// Required when Framework may skip <c>Tick</c> (master-off / dispose)
	/// or when aborting mid-pipeline (flag / territory).
	/// </summary>
	ImmediateClear,

	/// <summary>
	/// <see cref="CombatDecision"/> returns to Idle →
	/// <see cref="RsrEnableDecision.Decide"/> Stop via Tick.
	/// Do not dual-mutate the start latch with a separate Clear path.
	/// </summary>
	CombatPhaseTick,
}

/// <summary>
/// Pure stop gating for RSR (TASKS 6.5–6.7).
/// Shares soft-fail latch updates with <see cref="RsrEnableDecision"/> —
/// Clear and Tick must not invent a second broken-state machine.
/// </summary>
public static class RsrStopDecision
{
	/// <summary>
	/// Plugin abort path for this trigger.
	/// Death and mob-dead/combat-end use the combat-phase Tick path;
	/// flag / territory / master-off / dispose use ImmediateClear.
	/// </summary>
	public static RsrStopPath PathFor(RsrStopTrigger trigger)
		=> trigger switch
		{
			RsrStopTrigger.FlagChange => RsrStopPath.ImmediateClear,
			RsrStopTrigger.TerritoryLeave => RsrStopPath.ImmediateClear,
			RsrStopTrigger.MasterOff => RsrStopPath.ImmediateClear,
			RsrStopTrigger.Dispose => RsrStopPath.ImmediateClear,
			RsrStopTrigger.Death => RsrStopPath.CombatPhaseTick,
			RsrStopTrigger.CombatPhaseExit => RsrStopPath.CombatPhaseTick,
			_ => RsrStopPath.None,
		};

	/// <summary>True when Clear/Tick should attempt <c>RotationStop</c>.</summary>
	public static bool ShouldAttemptStop(bool rotationAutoStarted)
		=> rotationAutoStarted;

	/// <summary>
	/// Kind for an abort <see cref="RsrEnableHelper.Clear"/> attempt.
	/// Stop while latch held; else None. Soft-fail latch via
	/// <see cref="RsrEnableDecision.NextRotationAutoStarted"/>.
	/// </summary>
	public static RsrEnableKind DecideClear(bool rotationAutoStarted)
		=> rotationAutoStarted ? RsrEnableKind.Stop : RsrEnableKind.None;

	/// <summary>
	/// Tick path after combat phase may have fallen (death / mob dead / combat end).
	/// Delegates to <see cref="RsrEnableDecision.Decide"/> — single source of truth.
	/// </summary>
	public static RsrEnableKind DecideTick(bool inCombatPhase, bool rotationAutoStarted)
		=> RsrEnableDecision.Decide(inCombatPhase, rotationAutoStarted);

	/// <summary>Compact, side-effect-free stop-path diagnostic for helper logging.</summary>
	public static string Describe(RsrStopTrigger trigger, RsrStopPath path)
		=> $"trigger={trigger}, path={path}";
}
