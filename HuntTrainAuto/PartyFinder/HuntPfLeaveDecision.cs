#nullable enable
using System;

namespace HuntTrainAuto.PartyFinder;

/// <summary>Framework tick outcome for hunt-party auto-leave.</summary>
public enum HuntPfLeaveKind
{
	/// <summary>No leave this tick.</summary>
	None,

	/// <summary>Conductor LAST STOP armed; leave on combat→idle edge.</summary>
	LeaveAfterArmedCombatEnd,

	/// <summary>No new flag for idle timeout after last combat end.</summary>
	LeaveIdleTimeout,

	/// <summary>Party already gone — clear leave/join latches only.</summary>
	ClearLatchOnly,
}

/// <summary>
/// Pure gates for opt-in auto-leave when a hunt train ends.
/// Never leaves on bare combat end — requires LAST STOP arm or idle timeout.
/// </summary>
public static class HuntPfLeaveDecision
{
	/// <summary>Default idle leave: 10 minutes after last combat end with no new flag.</summary>
	public const int DefaultIdleLeaveMs = 600_000;

	public const int MinIdleLeaveMs = 60_000;
	public const int MaxIdleLeaveMs = 3_600_000;

	/// <summary>Throttle between /leave retries.</summary>
	public const int DefaultRetryIntervalMs = 3_000;

	public static int ClampIdleLeaveMs(int ms)
	{
		if (ms < MinIdleLeaveMs)
			return MinIdleLeaveMs;
		if (ms > MaxIdleLeaveMs)
			return MaxIdleLeaveMs;
		return ms;
	}

	public static bool IsActionReady(long nowMs, long nextActionMs)
		=> nowMs >= nextActionMs;

	public static long NextActionAt(long nowMs, int intervalMs = DefaultRetryIntervalMs)
		=> nowMs + Math.Max(0, intervalMs);

	/// <summary>
	/// Decide leave / clear for this tick.
	/// <paramref name="sessionActive"/>: PF joined latch or LAST STOP armed (do not leave random parties).
	/// </summary>
	public static HuntPfLeaveKind Decide(
		bool enabled,
		bool inParty,
		bool sessionActive,
		bool armedLastStop,
		bool wasInCombat,
		bool inCombat,
		long nowMs,
		long lastCombatEndMs,
		long lastFlagMs,
		int idleTimeoutMs,
		bool actionReady)
	{
		if (!enabled || !sessionActive)
			return HuntPfLeaveKind.None;

		if (!inParty)
			return HuntPfLeaveKind.ClearLatchOnly;

		if (!actionReady || inCombat)
			return HuntPfLeaveKind.None;

		if (armedLastStop && wasInCombat && !inCombat)
			return HuntPfLeaveKind.LeaveAfterArmedCombatEnd;

		var idleMs = ClampIdleLeaveMs(idleTimeoutMs);
		if (lastCombatEndMs <= 0)
			return HuntPfLeaveKind.None;

		// Idle from last combat end, but a newer flag resets the window.
		var idleAnchor = Math.Max(lastCombatEndMs, lastFlagMs);
		if (nowMs - idleAnchor >= idleMs)
			return HuntPfLeaveKind.LeaveIdleTimeout;

		return HuntPfLeaveKind.None;
	}

	/// <summary>True when combat falling edge should stamp lastCombatEndMs.</summary>
	public static bool ShouldNoteCombatEnd(bool wasInCombat, bool inCombat)
		=> wasInCombat && !inCombat;
}
