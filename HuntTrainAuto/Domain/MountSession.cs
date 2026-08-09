#nullable enable
using System;

namespace HuntTrainAuto.Domain;

/// <summary>Framework phases for post-TP auto-mount (HTA <c>TaskMount</c>).</summary>
public enum MountPhase
{
	Idle,
	/// <summary>Wait !Lifestream busy + screen ready + player ready + instance idle.</summary>
	WaitReady,
	/// <summary>Run <c>MountIfCan</c> until done.</summary>
	Mounting,
}

/// <summary>
/// Active Framework mount job (HTA <c>TaskMount.EnqueueIfEnabled</c> stand-in).
/// Survives <see cref="TeleportPlan"/> clear; advanced on Framework ticks.
/// </summary>
public sealed class MountSession
{
	public MountPhase Phase { get; private set; }

	public bool IsActive => Phase != MountPhase.Idle;

	/// <summary>CheckMount throttle deadline (<see cref="Environment.TickCount64"/>).</summary>
	public long NextCheckMs { get; set; }

	/// <summary>SummonMount throttle deadline.</summary>
	public long NextSummonMs { get; set; }

	/// <summary>Soft timeout deadline; 0 when idle.</summary>
	public long DeadlineMs { get; private set; }

	/// <summary>Avoid repeating fallback / no-mount warnings within one job.</summary>
	public bool WarnedFallback { get; set; }

	public bool WarnedNoMounts { get; set; }

	/// <summary>
	/// Start or replace a pending mount job.
	/// Arms a short <see cref="MountDecision.WaitReadyTimeoutMs"/> so WaitReady cannot pin
	/// Navigate when screen / instance-change blocks <c>CanBeginMountAttempt</c>.
	/// </summary>
	public void Enqueue(long nowMs)
	{
		Phase = MountPhase.WaitReady;
		NextCheckMs = 0;
		NextSummonMs = 0;
		DeadlineMs = nowMs + MountDecision.WaitReadyTimeoutMs;
		WarnedFallback = false;
		WarnedNoMounts = false;
	}

	/// <summary>Begin MountIfCan; refresh soft session timeout from the mounting start.</summary>
	public void EnterMounting(long nowMs, int timeoutMs = MountDecision.SessionTimeoutMs)
	{
		Phase = MountPhase.Mounting;
		DeadlineMs = nowMs + System.Math.Max(1_000, timeoutMs);
	}

	public void Clear()
	{
		Phase = MountPhase.Idle;
		NextCheckMs = 0;
		NextSummonMs = 0;
		DeadlineMs = 0;
		WarnedFallback = false;
		WarnedNoMounts = false;
	}
}
