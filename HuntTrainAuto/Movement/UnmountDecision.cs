#nullable enable

namespace HuntTrainAuto.Movement;

/// <summary>Framework tick outcome for TaskUnmount (inverse of <see cref="MountTickKind"/>).</summary>
public enum UnmountTickKind
{
	/// <summary>Done / skipped — clear the unmount job (may set ground-follow ready).</summary>
	Done,

	/// <summary>Wait another tick (throttle / transition / casting / path not ready).</summary>
	Wait,

	/// <summary>Dismount via GeneralAction 23 (no chat fallback — wrong GA /ac spams errors).</summary>
	Dismount,
}

/// <summary>Reason a <see cref="UnmountTickResult"/> did not dismount immediately.</summary>
public enum UnmountWaitReason
{
	None,
	TransitionOrCasting,
	CheckThrottle,
	ActionUnavailable,
	AnimationLocked,
	DismountThrottle,
}

/// <summary>Result of <see cref="UnmountDecision.DecideUnmountTick"/>.</summary>
public readonly struct UnmountTickResult
{
	public required UnmountTickKind Kind { get; init; }

	/// <summary>Why <see cref="Kind"/> is <see cref="UnmountTickKind.Wait"/>.</summary>
	public UnmountWaitReason WaitReason { get; init; }

	/// <summary>Force-extend CheckUnmount throttle while transitioning/casting.</summary>
	public bool ForceCheckThrottle { get; init; }

	/// <summary>
	/// Cooldown when <see cref="ForceCheckThrottle"/>; 0 → <see cref="UnmountDecision.CheckUnmountCooldownMs"/>.
	/// Unusable GA uses <see cref="UnmountDecision.ActionRetryCooldownMs"/>.
	/// </summary>
	public int ForceCheckCooldownMs { get; init; }

	/// <summary>
	/// Unmount finished successfully (already unmounted or dismount path completed) —
	/// subsequent nav should use ground follow (<see cref="PreferCanFlyForGroundFollow"/>).
	/// </summary>
	public bool ReadyForGroundFollow { get; init; }
}

/// <summary>
/// Pure unmount decision helpers (TASKS 4.12–4.14 / brief 4.6).
/// Inverse of <see cref="MountDecision"/>. Condition / ActionManager / chat wiring stays in the runner.
/// </summary>
public static class UnmountDecision
{
	/// <summary>GeneralAction id for Dismount (not 1 — that is Auto-attack).</summary>
	public const uint DismountGeneralActionId = 23;

	/// <summary>Throttle while transitioning/casting (mirror CheckMount 2000ms).</summary>
	public const int CheckUnmountCooldownMs = 2000;

	/// <summary>
	/// Retry when dismount GA is briefly unusable (flight / animation).
	/// Full <see cref="CheckUnmountCooldownMs"/> made land-and-unmount feel stuck.
	/// </summary>
	public const int ActionRetryCooldownMs = 250;

	/// <summary>Interval between dismount attempts.</summary>
	public const int DismountCooldownMs = 500;

	/// <summary>Human-readable decision outcome for runner debug logs.</summary>
	public static string Describe(UnmountTickResult result)
		=> result.Kind switch
		{
			UnmountTickKind.Done => result.ReadyForGroundFollow
				? "done (ground follow ready)"
				: "done",
			UnmountTickKind.Wait => $"wait ({result.WaitReason})",
			UnmountTickKind.Dismount => "dismount",
			_ => $"unknown ({result.Kind})",
		};

	/// <summary>
	/// Soft timeout while stuck in WaitReady (path / screen / instance gates).
	/// Shorter than <see cref="SessionTimeoutMs"/> — WaitReady used DeadlineMs=0 and could
	/// pin forever after fly PathStop, blocking Navigate remount/descent.
	/// </summary>
	public const int WaitReadyTimeoutMs = 8_000;

	/// <summary>Soft session timeout once Unmounting has begun.</summary>
	public const int SessionTimeoutMs = 60_000;

	/// <summary>
	/// After unmount success, ground approach to the mob should use <c>canFly: false</c>.
	/// Exposed here so callers need not hard-code the preference.
	/// </summary>
	public const bool PreferCanFlyForGroundFollow = false;

	/// <summary>
	/// Navigate → Unmount only after arrival <b>and</b> dismount when auto-unmount is on.
	/// Staying in Navigate while Mounted/InFlight keeps mid-air PathStop/descent recovery alive.
	/// </summary>
	public static bool ShouldFlagArrived(
		bool withinFlagArrival,
		bool autoUnmountAtFlag,
		bool mountedOrInFlight,
		bool readyForGroundFollow)
	{
		if (!withinFlagArrival)
			return false;

		if (!autoUnmountAtFlag)
			return !mountedOrInFlight;

		// Auto-unmount: wait for ReadyForGroundFollow while still mounted/flying.
		// Already on foot → advance (unmount skip / latch may catch up same tick).
		return readyForGroundFollow || !mountedOrInFlight;
	}

	/// <summary>HTA-style gate on <see cref="Configuration.AutoUnmountAtFlag"/>.</summary>
	public static bool ShouldEnqueueIfEnabled(bool autoUnmountAtFlag) => autoUnmountAtFlag;

	/// <summary>
	/// Start a new unmount job only when config allows and no job is already running.
	/// Divert used to call enqueue every tick and reset WaitReady — dismount never progressed.
	/// </summary>
	public static bool ShouldStartUnmountJob(bool autoUnmountAtFlag, bool alreadyActive)
		=> autoUnmountAtFlag && !alreadyActive;

	/// <summary>
	/// One-shot enqueue when flag arrival is signaled and no job is already active/latched.
	/// </summary>
	public static bool ShouldEnqueueOnArrival(
		bool autoUnmountAtFlag,
		bool isArrived,
		bool alreadyActiveOrEnqueued)
		=> autoUnmountAtFlag && isArrived && !alreadyActiveOrEnqueued;

	/// <summary>
	/// Path / arrival gate: path stopped for this arrival, arrival already signaled, or path not running.
	/// </summary>
	public static bool IsPathReadyForUnmount(
		bool pathRunning,
		bool arrivalSignaled,
		bool pathStoppedForArrival)
		=> pathStoppedForArrival || arrivalSignaled || !pathRunning;

	/// <summary>
	/// Wait-for-ready before UnmountIfCan: path/arrival ready, screen ready (not between-areas),
	/// player ready, no mid-TP plan, instance-change idle.
	/// </summary>
	public static bool CanBeginUnmountAttempt(
		bool pathReadyForUnmount,
		bool screenReady,
		bool playerReady,
		bool teleportPlanActive,
		bool instanceChangeActive)
		=> pathReadyForUnmount
			&& screenReady
			&& playerReady
			&& !teleportPlanActive
			&& !instanceChangeActive;

	/// <summary>Already unmounted → success / no-op.</summary>
	public static bool IsUnmountCompleteOrSkipped(bool mounted) => !mounted;

	/// <summary>Force CheckUnmount throttle while in mount transition or casting.</summary>
	public static bool NeedsTransitionWait(bool mountOrOrnamentTransition, bool casting)
		=> mountOrOrnamentTransition || casting;

	/// <summary>GeneralAction Dismount (23) usable when status == 0.</summary>
	public static bool IsDismountActionUsable(uint actionStatus) => actionStatus == 0;

	/// <summary>
	/// One UnmountIfCan decision step after early complete/skip checks.
	/// Caller applies throttles from <see cref="ForceCheckThrottle"/> / dismount fire.
	/// </summary>
	public static UnmountTickResult DecideUnmountTick(
		bool mounted,
		bool mountOrOrnamentTransition,
		bool casting,
		bool checkThrottleReady,
		bool dismountActionUsable,
		bool animationLocked,
		bool dismountThrottleReady)
	{
		if (IsUnmountCompleteOrSkipped(mounted))
		{
			return new UnmountTickResult
			{
				Kind = UnmountTickKind.Done,
				ReadyForGroundFollow = true,
			};
		}

		var forceCheck = NeedsTransitionWait(mountOrOrnamentTransition, casting);
		if (forceCheck || !checkThrottleReady)
		{
			return new UnmountTickResult
			{
				Kind = UnmountTickKind.Wait,
				ForceCheckThrottle = forceCheck,
				WaitReason = forceCheck
					? UnmountWaitReason.TransitionOrCasting
					: UnmountWaitReason.CheckThrottle,
			};
		}

		// Transient GA status (flight / animation / AM null) — retry, do not fire.
		// Firing UseAction + /ac while unusable spammed "Cannot execute at this time".
		if (!dismountActionUsable)
		{
			return new UnmountTickResult
			{
				Kind = UnmountTickKind.Wait,
				ForceCheckThrottle = true,
				ForceCheckCooldownMs = ActionRetryCooldownMs,
				WaitReason = UnmountWaitReason.ActionUnavailable,
			};
		}

		if (animationLocked || !dismountThrottleReady)
		{
			return new UnmountTickResult
			{
				Kind = UnmountTickKind.Wait,
				WaitReason = animationLocked
					? UnmountWaitReason.AnimationLocked
					: UnmountWaitReason.DismountThrottle,
			};
		}

		return new UnmountTickResult { Kind = UnmountTickKind.Dismount };
	}

	/// <summary>Whether CheckUnmount throttle allows progress.</summary>
	public static bool IsCheckReady(long nextCheckMs, long nowMs) => nowMs >= nextCheckMs;

	/// <summary>Force CheckUnmount deadline (extend-only vs existing).</summary>
	public static long ForceCheckThrottle(long nextCheckMs, long nowMs, int cooldownMs = CheckUnmountCooldownMs)
		=> System.Math.Max(nextCheckMs, nowMs + System.Math.Max(0, cooldownMs));

	/// <summary>Whether Dismount throttle allows an attempt.</summary>
	public static bool TryFireDismount(ref long nextDismountMs, long nowMs, int cooldownMs = DismountCooldownMs)
	{
		if (nowMs < nextDismountMs)
			return false;

		nextDismountMs = nowMs + System.Math.Max(0, cooldownMs);
		return true;
	}

	/// <summary>Session exceeded soft timeout.</summary>
	public static bool IsSessionTimedOut(long deadlineMs, long nowMs)
		=> deadlineMs > 0 && nowMs >= deadlineMs;

}
