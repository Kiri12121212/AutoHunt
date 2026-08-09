#nullable enable

namespace HuntTrainAuto.State;

/// <summary>
/// Territory-change cleanup mode (TASKS 7.3).
/// Distinguishes intentional TP arrival into hunting territory from leave / non-hunting.
/// </summary>
public enum TerritoryCleanupKind
{
	/// <summary>
	/// Still in a hunting territory with no active TP plan — no territory-driven abort.
	/// </summary>
	StayHuntingNoop = 0,

	/// <summary>
	/// Active teleport plan cleared into a hunting territory: instance-change + mount handoff.
	/// Clears engage/combat/RSR/path latches; does not reset the train controller or clear conductors/flag.
	/// </summary>
	TpArrivalHandoff,

	/// <summary>
	/// Hunting territory entered without an active TP plan (BetweenAreas cleared early, etc.).
	/// Stop stale path and invalidate flag WorldPos so Navigate re-resolves against the new mesh.
	/// </summary>
	HuntingMeshReload,

	/// <summary>
	/// Arrived in a non-hunting territory (left hunt zone or TP landed wrong): full pipeline abort.
	/// </summary>
	LeaveHuntingFull,
}

/// <summary>
/// Flags describing which pipeline pieces Plugin should touch for a territory change.
/// Pure data — no IPC / Dalamud. Soft-fail wiring lives in Plugin.
/// </summary>
public readonly struct TerritoryCleanupPlan
{
	public required TerritoryCleanupKind Kind { get; init; }

	/// <summary>Clear <c>TeleportPlan</c> remnants.</summary>
	public bool ClearTeleportPlan { get; init; }

	/// <summary>
	/// Enqueue instance change when plan instance needs it and territory matches the plan
	/// (caller still applies <c>TeleportGate</c> + territory equality).
	/// </summary>
	public bool EnqueueInstanceChangeIfNeeded { get; init; }

	/// <summary>
	/// Remount after TP only when still on foot (TP often keeps mount).
	/// Caller should skip enqueue when already Mounted/InFlight.
	/// </summary>
	public bool EnqueueMount { get; init; }

	public bool ClearInstanceChange { get; init; }
	public bool ClearMount { get; init; }
	public bool ClearActiveHuntFlag { get; init; }
	public bool ClearFlagArrival { get; init; }
	public bool ClearUnmount { get; init; }
	public bool ClearEngage { get; init; }
	public bool ClearCombat { get; init; }
	public bool ClearRsr { get; init; }

	/// <summary>
	/// Soft-stop vnavmesh path. Prefer this over forcing dismount mid-zone-load —
	/// jobs are cleared; mount state is left to the game during load screens.
	/// </summary>
	public bool StopNavPath { get; init; }

	/// <summary>
	/// Clear <see cref="HuntFlag.WorldPos"/> so PointOnFloor re-runs against the new mesh
	/// after territory change (stale floor hits from the previous zone break pathfind).
	/// </summary>
	public bool InvalidateFlagWorldPos { get; init; }

	public bool ClearConductors { get; init; }
	public bool ResetTrainController { get; init; }
	public bool SaveConfig { get; init; }
}

/// <summary>
/// Pure leave-vs-TP-arrival decisions for <c>OnTerritoryChanged</c> (TASKS 7.3).
/// No Dalamud types. Soft-fail: never throws.
/// </summary>
public static class TerritoryCleanupDecision
{
	/// <summary>
	/// Decide cleanup for the new territory.
	/// <paramref name="teleportPlanActive"/> is the plan state <b>before</b> any clear.
	/// <paramref name="isHuntingTerritory"/> is the destination (new) territory.
	/// <paramref name="hasActiveHuntFlag"/>: when BetweenAreas already cleared the TP plan,
	/// hunting→hunting still needs mesh reload handoff (invalidate WorldPos / stop path).
	/// </summary>
	public static TerritoryCleanupPlan Decide(
		bool teleportPlanActive,
		bool isHuntingTerritory,
		bool hasActiveHuntFlag = false)
	{
		if (teleportPlanActive && isHuntingTerritory)
			return TpArrivalHandoff();

		if (!isHuntingTerritory)
			return LeaveHuntingFull();

		if (hasActiveHuntFlag)
			return HuntingMeshReload();

		return StayHuntingNoop();
	}

	public static TerritoryCleanupPlan StayHuntingNoop()
		=> new()
		{
			Kind = TerritoryCleanupKind.StayHuntingNoop,
		};

	/// <summary>
	/// Hunting territory entered without an active TP plan (e.g. BetweenAreas cleared early).
	/// Keep train / mount handoff; refresh nav against the new mesh.
	/// </summary>
	public static TerritoryCleanupPlan HuntingMeshReload()
		=> new()
		{
			Kind = TerritoryCleanupKind.HuntingMeshReload,
			StopNavPath = true,
			InvalidateFlagWorldPos = true,
			ClearFlagArrival = true,
		};

	public static TerritoryCleanupPlan TpArrivalHandoff()
		=> new()
		{
			Kind = TerritoryCleanupKind.TpArrivalHandoff,
			ClearTeleportPlan = true,
			EnqueueInstanceChangeIfNeeded = true,
			// Remount only if land wiped mount; MountRunner skips when already mounted.
			EnqueueMount = true,
			// Path may still be running from the previous zone; stop without aborting handoff.
			StopNavPath = true,
			InvalidateFlagWorldPos = true,
			ClearEngage = true,
			ClearCombat = true,
			ClearRsr = true,
			ClearFlagArrival = true,
			ClearUnmount = true,
			// Mount / instance are the handoff — do not clear them after enqueue.
			// Train stays in Teleport until Framework sees plan cleared → Navigate.
		};

	public static TerritoryCleanupPlan LeaveHuntingFull()
		=> new()
		{
			Kind = TerritoryCleanupKind.LeaveHuntingFull,
			ClearTeleportPlan = true,
			ClearInstanceChange = true,
			ClearMount = true,
			ClearActiveHuntFlag = true,
			ClearFlagArrival = true,
			ClearUnmount = true,
			ClearEngage = true,
			ClearCombat = true,
			ClearRsr = true,
			// Dismount-safe: stop path + clear jobs; do not force dismount mid-load.
			StopNavPath = true,
			ClearConductors = true,
			ResetTrainController = true,
			SaveConfig = true,
		};
}
