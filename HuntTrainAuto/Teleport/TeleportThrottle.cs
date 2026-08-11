#nullable enable
using System;

namespace HuntTrainAuto.Teleport;

/// <summary>
/// Pure throttle helpers for Framework teleport retries (HTA <c>EzThrottler</c> "Teleport" key).
/// State is owned by the caller (no static clocks).
/// </summary>
public static class TeleportThrottle
{
	/// <summary>Compact, side-effect-free throttle diagnostic for call-site logging.</summary>
	public static string Describe(long nextAllowedMs, long nowMs)
		=> $"throttle: ready={IsReady(nextAllowedMs, nowMs)}, remainingMs={RemainingMs(nextAllowedMs, nowMs)}";

	/// <summary>Default cooldown after a successful <see cref="TryFire"/> (HTA default interval).</summary>
	public const int DefaultCooldownMs = 500;

	/// <summary>HTA: teleport cast (action 5) while <c>ConditionFlag.Casting</c> is set.</summary>
	public const int CastingTeleportCooldownMs = 500;

	/// <summary>HTA: teleport cast finished but still <c>IsCasting</c> (cast-bar edge).</summary>
	public const int PostTeleportCastCooldownMs = 2000;

	/// <summary>HTA: non-teleport cast or mount transition.</summary>
	public const int SoftWaitCooldownMs = 500;

	/// <summary>Teleport action id (Return / Teleport).</summary>
	public const uint TeleportCastActionId = 5;

	/// <summary>True when <paramref name="nowMs"/> has reached or passed the next-allowed tick.</summary>
	public static bool IsReady(long nextAllowedMs, long nowMs)
		=> nowMs >= nextAllowedMs;

	/// <summary>Remaining milliseconds until ready (0 when ready).</summary>
	public static int RemainingMs(long nextAllowedMs, long nowMs)
		=> (int)Math.Max(0L, nextAllowedMs - nowMs);

	/// <summary>
	/// Force-extend the throttle (HTA <c>EzThrottler.Throttle(name, ms, true)</c>).
	/// </summary>
	public static long Force(long nowMs, int cooldownMs)
		=> nowMs + Math.Max(0, cooldownMs);

	/// <summary>
	/// If ready, arms a new cooldown and returns true (HTA <c>EzThrottler.Throttle(name)</c>).
	/// </summary>
	public static bool TryFire(ref long nextAllowedMs, long nowMs, int cooldownMs = DefaultCooldownMs)
	{
		if (nowMs < nextAllowedMs)
			return false;

		nextAllowedMs = Force(nowMs, cooldownMs);
		return true;
	}

	/// <summary>
	/// Casting / mount soft-wait: extends <paramref name="nextAllowedMs"/> when a soft-wait applies.
	/// Never shortens an existing deadline (same extend-only rule as <see cref="ApplyPreDelay"/>).
	/// Returns null when no soft-wait applies (caller keeps prior deadline).
	/// </summary>
	public static long? SoftWaitNextAllowed(
		long nextAllowedMs,
		long nowMs,
		bool isCasting,
		uint castActionId,
		bool conditionCasting,
		bool mountOrOrnamentTransition)
	{
		long? candidate = null;
		if (isCasting)
		{
			if (castActionId == TeleportCastActionId)
			{
				candidate = Force(
					nowMs,
					conditionCasting ? CastingTeleportCooldownMs : PostTeleportCastCooldownMs);
			}
			else
			{
				candidate = Force(nowMs, SoftWaitCooldownMs);
			}
		}
		else if (mountOrOrnamentTransition)
		{
			candidate = Force(nowMs, SoftWaitCooldownMs);
		}

		if (candidate == null)
			return null;

		return Math.Max(nextAllowedMs, candidate.Value);
	}

	/// <summary>
	/// HTA <c>Utils.DelayTeleport</c>: optional random pre-delay before first attempt.
	/// <paramref name="randomOffset"/> is <c>Random.Next(max - min)</c> (caller supplies).
	/// Extends throttle only when remaining time is shorter than the chosen delay.
	/// </summary>
	public static long ApplyPreDelay(
		long nextAllowedMs,
		long nowMs,
		bool enabled,
		int delayMinMs,
		int delayMaxMs,
		int randomOffset)
	{
		// Fresh adopt: do not inherit a stale SoftWait / TryFire deadline from a prior hop.
		if (!enabled)
			return nowMs;

		if (delayMaxMs <= 0 || delayMaxMs < delayMinMs)
			return nowMs;

		var delay = delayMinMs + Math.Max(0, randomOffset);
		if (RemainingMs(nextAllowedMs, nowMs) < delay)
			return Force(nowMs, delay);

		return nextAllowedMs;
	}
}
