#nullable enable
using System;

namespace HuntTrainAuto.Movement;

/// <summary>Soft-retry gate for <c>SimpleMove.PathfindAndMoveTo</c> after territory / mesh load.</summary>
public enum MeshPathfindRetryKind
{
	/// <summary>Player / nav / in-progress guards — do not burn an attempt.</summary>
	WaitNotReady,

	/// <summary>Cooldown after a prior start (pathfind failed or IPC soft-fail).</summary>
	WaitCooldown,

	/// <summary>Safe to call <c>PathfindAndMoveTo</c> this tick.</summary>
	Start,

	/// <summary>Gave up after <see cref="MeshPathfindRetryDecision.MaxAttempts"/> — stop spamming.</summary>
	Exhausted,
}

/// <summary>
/// Pure soft-retry for mesh pathfind starts after zone swap.
/// <see cref="MovementDecision"/> still owns ready / in-progress guards; this throttles
/// repeated StartMeshPath when vnav accepts then fails (<c>poly → 0</c>) or IPC returns false.
/// </summary>
public static class MeshPathfindRetryDecision
{
	/// <summary>Minimum gap between pathfind start attempts (ms).</summary>
	public const int RetryCooldownMs = 1_000;

	/// <summary>
	/// Cap attempts per territory epoch so a permanently unreachable dest does not spam forever.
	/// ~30s of retries at <see cref="RetryCooldownMs"/> after mesh ready.
	/// </summary>
	public const int MaxAttempts = 30;

	/// <summary>
	/// Decide whether to fire another <c>PathfindAndMoveTo</c>.
	/// <paramref name="canStartMeshPathfind"/> is <see cref="MovementDecision.CanStartMeshPathfind"/>
	/// (includes player-on-mesh when the caller probes it).
	/// </summary>
	public static MeshPathfindRetryKind Decide(
		bool canStartMeshPathfind,
		long nowMs,
		long nextAttemptMs,
		int attempts,
		int maxAttempts = MaxAttempts)
	{
		if (!canStartMeshPathfind)
			return MeshPathfindRetryKind.WaitNotReady;

		if (attempts >= maxAttempts)
			return MeshPathfindRetryKind.Exhausted;

		if (nowMs < nextAttemptMs)
			return MeshPathfindRetryKind.WaitCooldown;

		return MeshPathfindRetryKind.Start;
	}

	/// <summary>
	/// After a Start attempt: bump attempt count and arm cooldown.
	/// Always throttles — even when IPC returns true — so a silent pathfind fail
	/// cannot re-queue every Framework tick.
	/// </summary>
	public static void AfterStartAttempt(
		ref long nextAttemptMs,
		ref int attempts,
		long nowMs,
		int cooldownMs = RetryCooldownMs)
	{
		attempts++;
		nextAttemptMs = nowMs + Math.Max(0, cooldownMs);
	}

	/// <summary>
	/// Reset retry bookkeeping (new flag, territory change, successful path running).
	/// </summary>
	public static void Reset(ref long nextAttemptMs, ref int attempts)
	{
		nextAttemptMs = 0;
		attempts = 0;
	}

	/// <summary>
	/// True when a path is actually following — clear retry counters so the next
	/// interruption gets a fresh budget. Do <b>not</b> reset on pathfind-in-progress alone:
	/// silent <c>poly → 0</c> failures briefly set in-progress then fail, which would
	/// wipe the cooldown every attempt.
	/// </summary>
	public static bool ShouldResetOnNavProgress(bool pathIsRunning, int numWaypoints)
		=> pathIsRunning || numWaypoints > 0;
}
