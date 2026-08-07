#nullable enable

namespace HuntTrainAuto.State;

/// <summary>
/// New-conductor-flag restart mode (TASKS 7.4).
/// Distinguishes Idle start from abort-then-restart when mid-pipeline.
/// </summary>
public enum FlagRestartKind
{
	/// <summary>
	/// Controller Idle and no in-flight work — clear stale jobs then Apply Start*.
	/// </summary>
	StartFromIdle = 0,

	/// <summary>
	/// Active phase and/or in-flight nav/follow/combat/RSR/mount/TP:
	/// stop path + clear jobs + reset to Idle, then Apply Start* for the new flag.
	/// </summary>
	AbortThenRestart,
}

/// <summary>
/// Flags describing abort + restart side effects for a new conductor flag.
/// Pure data — no IPC / Dalamud. Soft-fail wiring lives in Plugin.
/// </summary>
public readonly struct FlagRestartPlan
{
	public required FlagRestartKind Kind { get; init; }

	/// <summary>Soft-stop vnavmesh path from the previous flag leg.</summary>
	public bool StopNavPath { get; init; }

	public bool ClearFollow { get; init; }
	public bool ClearInstanceChange { get; init; }
	public bool ClearMount { get; init; }
	public bool ClearFlagArrival { get; init; }
	public bool ClearUnmount { get; init; }
	public bool ClearEngage { get; init; }
	public bool ClearCombat { get; init; }
	public bool ClearRsr { get; init; }

	/// <summary>Force train controller to Idle before <see cref="StartEvent"/>.</summary>
	public bool ResetTrainController { get; init; }

	/// <summary>
	/// Idle start after abort (or from Idle). <see cref="HuntTrainEvent.None"/> when master off / no plan.
	/// </summary>
	public HuntTrainEvent StartEvent { get; init; }
}

/// <summary>
/// Pure abort-vs-start decisions for <c>OnHuntFlagReceived</c> (TASKS 7.4).
/// No Dalamud types. Soft-fail: never throws.
/// </summary>
public static class FlagRestartDecision
{
	/// <summary>
	/// True when the train phase is non-Idle or callers report leftover in-flight work
	/// (nav / follow / combat / RSR / mount / TP / instance / unmount).
	/// </summary>
	public static bool IsPipelineActive(HuntTrainPhase phase, bool hasInFlightWork)
		=> phase != HuntTrainPhase.Idle || hasInFlightWork;

	/// <summary>
	/// Decide abort-then-restart vs start-from-idle for a newly adopted flag.
	/// <paramref name="teleportPlanActive"/> / <paramref name="alreadyCloseSkip"/> are post-adopt
	/// observables used only for <see cref="HuntTrainObserve.DecideFlagStart"/>.
	/// <paramref name="pipelineActive"/> is the pre-clear snapshot (phase or in-flight work).
	/// </summary>
	public static FlagRestartPlan Decide(
		bool pluginEnabled,
		bool pipelineActive,
		bool teleportPlanActive,
		bool alreadyCloseSkip,
		bool useMount)
	{
		var start = HuntTrainObserve.DecideFlagStart(
			pluginEnabled,
			teleportPlanActive,
			alreadyCloseSkip,
			useMount);

		return pipelineActive
			? AbortThenRestart(start)
			: StartFromIdle(start);
	}

	/// <summary>
	/// Convenience: derive <paramref name="pipelineActive"/> from phase + in-flight, then <see cref="Decide"/>.
	/// </summary>
	public static FlagRestartPlan Decide(
		bool pluginEnabled,
		HuntTrainPhase phase,
		bool hasInFlightWork,
		bool teleportPlanActive,
		bool alreadyCloseSkip,
		bool useMount)
		=> Decide(
			pluginEnabled,
			IsPipelineActive(phase, hasInFlightWork),
			teleportPlanActive,
			alreadyCloseSkip,
			useMount);

	public static FlagRestartPlan StartFromIdle(HuntTrainEvent startEvent)
		=> new()
		{
			Kind = FlagRestartKind.StartFromIdle,
			// New flag always invalidates prior job latches (even from Idle).
			ClearInstanceChange = true,
			ClearMount = true,
			ClearFlagArrival = true,
			ClearUnmount = true,
			ClearEngage = true,
			ClearCombat = true,
			ClearRsr = true,
			StartEvent = startEvent,
		};

	public static FlagRestartPlan AbortThenRestart(HuntTrainEvent startEvent)
		=> new()
		{
			Kind = FlagRestartKind.AbortThenRestart,
			StopNavPath = true,
			ClearFollow = true,
			ClearInstanceChange = true,
			ClearMount = true,
			ClearFlagArrival = true,
			ClearUnmount = true,
			ClearEngage = true,
			ClearCombat = true,
			ClearRsr = true,
			ResetTrainController = true,
			StartEvent = startEvent,
		};
}
