#nullable enable
using System;

namespace HuntTrainAuto.Contracts;

/// <summary>Rotation Solver Reborn IPC surface (AD <c>RSR_IPCSubscriber</c>).</summary>
public interface IRsrService : IDisposable
{
	/// <summary>
	/// True when Dalamud reports plugin InternalName <c>RotationSolver</c> as loaded
	/// (AD <c>IPCSubscriber_Common.IsReady("RotationSolver")</c>).
	/// </summary>
	bool IsEnabled { get; }

	/// <summary>
	/// True when the <c>RotationSolverReborn.AutorotationActive</c> CallGate is reachable.
	/// Soft-fails: never throws.
	/// </summary>
	bool IsAvailable { get; }

	/// <summary>
	/// Apply default combat settings then start AutoDuty mode
	/// (<c>AutodutyChangeOperatingMode</c>). Soft-fails silently.
	/// Defaults: HostileType AllTargetsCanAttack, FriendlyPartyNpcHealRaise3 true,
	/// AutoOffAfterCombat false, targeting LowHP.
	/// </summary>
	void RotationAuto();

	/// <summary>
	/// Stop rotation (<c>ChangeOperatingMode(Off)</c>). Soft-fails silently.
	/// </summary>
	void RotationStop();
}
