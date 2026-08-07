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
	/// Apply combat settings then start AutoDuty mode
	/// (<c>AutodutyChangeOperatingMode</c>). Soft-fails silently.
	/// Settings: HostileType from <paramref name="hostileType"/>,
	/// FriendlyPartyNpcHealRaise3 true, AutoOffAfterCombat false,
	/// then Autoduty mode with <paramref name="targeting"/>.
	/// </summary>
	/// <returns>True when the AutoDuty CallGate invoke succeeded.</returns>
	bool RotationAuto(RsrTargetingType targeting, RsrTargetHostileType hostileType);

	/// <summary>
	/// Stop rotation (<c>ChangeOperatingMode(Off)</c>). Soft-fails silently.
	/// </summary>
	/// <returns>True when the Off CallGate invoke succeeded.</returns>
	bool RotationStop();
}
