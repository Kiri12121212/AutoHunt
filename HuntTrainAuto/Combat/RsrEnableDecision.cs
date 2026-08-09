#nullable enable

namespace HuntTrainAuto.Combat;

/// <summary>Framework tick outcome for RSR enable gating (TASKS 6.2).</summary>
public enum RsrEnableKind
{
	/// <summary>No IPC call this tick.</summary>
	None,

	/// <summary>
	/// Start (or retry) AutoDuty — while in combat phase until IPC reports success.
	/// Not fired on flag arrival / unmount / Following alone.
	/// </summary>
	StartAuto,

	/// <summary>
	/// Stop (or retry) rotation — while out of combat phase if we believe RSR is on,
	/// so enable is not sticky after a soft-failed stop.
	/// Abort Clear paths (flag / territory / master-off / dispose) use the same kind
	/// via <see cref="RsrStopDecision.DecideClear"/>.
	/// </summary>
	Stop,
}

/// <summary>
/// Pure edge-trigger + soft-fail retry for RSR enable (TASKS 6.2).
/// Observes <see cref="CombatSession.InCombatPhase"/> only — does not re-decide engage.
/// Success latch <c>rotationAutoStarted</c> preserves one-shot intent after IPC succeeds.
/// </summary>
public static class RsrEnableDecision
{
	/// <summary>
	/// Gate from combat-phase latch and last known IPC success.
	/// </summary>
	/// <param name="inCombatPhase">Current <see cref="CombatSession.InCombatPhase"/>.</param>
	/// <param name="rotationAutoStarted">True after a successful <c>RotationAuto</c> until a successful <c>RotationStop</c>.</param>
	public static RsrEnableKind Decide(bool inCombatPhase, bool rotationAutoStarted)
	{
		// Rising edge or retry after failed enable — never spam once started.
		if (inCombatPhase && !rotationAutoStarted)
			return RsrEnableKind.StartAuto;
		// Falling edge or retry after failed stop — keep trying while we believe RSR is on.
		if (!inCombatPhase && rotationAutoStarted)
			return RsrEnableKind.Stop;
		return RsrEnableKind.None;
	}

	/// <summary>
	/// Next success latch after an IPC attempt (or no-op). Unchanged when IPC soft-fails.
	/// </summary>
	public static bool NextRotationAutoStarted(
		RsrEnableKind kind,
		bool ipcSucceeded,
		bool rotationAutoStarted)
	{
		if (!ipcSucceeded)
			return rotationAutoStarted;
		return kind switch
		{
			RsrEnableKind.StartAuto => true,
			RsrEnableKind.Stop => false,
			_ => rotationAutoStarted,
		};
	}

	/// <summary>Compact, side-effect-free RSR decision diagnostic for helper logging.</summary>
	public static string Describe(RsrEnableKind kind)
		=> $"action={kind}";
}
