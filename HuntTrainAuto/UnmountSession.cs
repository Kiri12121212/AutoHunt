#nullable enable

namespace HuntTrainAuto;

/// <summary>Framework phases for post-arrival auto-unmount (TASKS TaskUnmount).</summary>
public enum UnmountPhase
{
	Idle,
	/// <summary>Wait path/arrival ready + screen ready + player ready + no mid-TP.</summary>
	WaitReady,
	/// <summary>Run UnmountIfCan until done.</summary>
	Unmounting,
}

/// <summary>
/// Active Framework unmount job (inverse of <see cref="MountSession"/>).
/// <see cref="ReadyForGroundFollow"/> survives job <see cref="Clear"/> until <see cref="ClearGroundFollow"/> / new flag.
/// </summary>
public sealed class UnmountSession
{
	public UnmountPhase Phase { get; private set; }

	public bool IsActive => Phase != UnmountPhase.Idle;

	/// <summary>CheckUnmount throttle deadline (<see cref="System.Environment.TickCount64"/>).</summary>
	public long NextCheckMs { get; set; }

	/// <summary>Dismount attempt throttle deadline.</summary>
	public long NextDismountMs { get; set; }

	/// <summary>Soft timeout deadline; 0 when idle / WaitReady.</summary>
	public long DeadlineMs { get; private set; }

	/// <summary>
	/// After successful unmount (or already-unmounted skip), subsequent nav should use
	/// <see cref="UnmountDecision.PreferCanFlyForGroundFollow"/> (<c>canFly: false</c>).
	/// Survives job clear; reset on new flag via <see cref="ClearGroundFollow"/>.
	/// </summary>
	public bool ReadyForGroundFollow { get; private set; }

	/// <summary>Start or replace a pending unmount job. Deadline starts only when unmounting begins.</summary>
	public void Enqueue(long nowMs)
	{
		_ = nowMs;
		Phase = UnmountPhase.WaitReady;
		NextCheckMs = 0;
		NextDismountMs = 0;
		DeadlineMs = 0;
	}

	/// <summary>Begin UnmountIfCan; arms the soft session timeout (excludes WaitReady).</summary>
	public void EnterUnmounting(long nowMs)
	{
		Phase = UnmountPhase.Unmounting;
		DeadlineMs = nowMs + UnmountDecision.SessionTimeoutMs;
	}

	/// <summary>Mark ground-follow prep ready (phase 5 uses <c>canFly: false</c>).</summary>
	public void MarkGroundFollowReady() => ReadyForGroundFollow = true;

	/// <summary>Clear ground-follow signal (new flag / abort).</summary>
	public void ClearGroundFollow() => ReadyForGroundFollow = false;

	/// <summary>Clear the unmount job; keeps <see cref="ReadyForGroundFollow"/>.</summary>
	public void Clear()
	{
		Phase = UnmountPhase.Idle;
		NextCheckMs = 0;
		NextDismountMs = 0;
		DeadlineMs = 0;
	}

	/// <summary>Clear job + ground-follow latch (new hunt flag).</summary>
	public void ClearAll()
	{
		Clear();
		ClearGroundFollow();
	}
}
