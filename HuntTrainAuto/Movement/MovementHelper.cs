#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;

namespace HuntTrainAuto.Movement;

/// <summary>
/// Thin game wiring for AD <c>MovementHelper.Move</c> (hunt-train adapted).
/// Pure decisions live in <see cref="MovementDecision"/>. Soft-fails; never throws to callers.
/// Does not auto-mount (see <see cref="MountRunner"/>) or sprint/peloton.
/// </summary>
public sealed class MovementHelper
{
	private readonly IVnavmeshService vnav;
	private readonly IChatOutput chat;
	private readonly IObjectTable objectTable;
	private readonly IDataManager dataManager;
	private readonly ICondition condition;
	private readonly IClientState clientState;
	private readonly IPluginLog pluginLog;
	private long nextTakeoffMs;
	private long nextMeshPathfindAttemptMs;
	private int meshPathfindAttempts;
	private bool meshPathfindExhaustedLogged;

	public MovementHelper(
		IVnavmeshService vnav,
		IChatOutput chat,
		IObjectTable objectTable,
		IDataManager dataManager,
		ICondition condition,
		IClientState clientState,
		IPluginLog pluginLog)
	{
		this.vnav = vnav;
		this.chat = chat;
		this.objectTable = objectTable;
		this.dataManager = dataManager;
		this.condition = condition;
		this.clientState = clientState;
		this.pluginLog = pluginLog;
	}

	/// <summary>Stop active vnavmesh path following.</summary>
	public void Stop() => vnav.PathStop();

	/// <summary>
	/// Clear soft-retry bookkeeping after territory change / new flag so the next Navigate
	/// epoch gets a fresh attempt budget.
	/// </summary>
	public void ResetMeshPathfindRetry()
	{
		MeshPathfindRetryDecision.Reset(ref nextMeshPathfindAttemptMs, ref meshPathfindAttempts);
		meshPathfindExhaustedLogged = false;
	}

	/// <summary>
	/// Whether the current territory supports flying (AD <c>IsFlyingSupported</c> without ECommons).
	/// Soft-fails to false.
	/// </summary>
	public bool IsFlyingSupported()
	{
		try
		{
			var territoryId = clientState.TerritoryType;
			if (territoryId == 0)
				return false;

			var row = dataManager.GetExcelSheet<TerritoryType>()?.GetRowOrDefault(territoryId);
			if (row == null)
				return false;

			var intendedUse = row.Value.TerritoryIntendedUse.RowId;
			var aetherSet = row.Value.AetherCurrentCompFlgSet.RowId;
			return MovementDecision.ZoneSupportsFlying(
				territoryId,
				intendedUse,
				IsAetherCurrentZoneComplete(aetherSet));
		}
		catch (Exception ex)
		{
			pluginLog.Debug($"IsFlyingSupported soft-fail: {ex.Message}");
			return false;
		}
	}

	/// <summary>
	/// Move toward <paramref name="position"/>. Returns true when arrived (within last-point tolerance).
	/// Call each Framework tick while navigating.
	/// </summary>
	/// <param name="position">World destination.</param>
	/// <param name="tolerance">Path-follow tolerance when starting a mesh pathfind.</param>
	/// <param name="lastPointTolerance">Arrival / final-waypoint tolerance.</param>
	/// <param name="fly">Requested fly; forced ground when zone does not support flying.</param>
	/// <param name="useMesh">True = <c>SimpleMove.PathfindAndMoveTo</c>; false = direct <c>Path.MoveTo</c>.</param>
	public bool Move(
		Vector3 position,
		float tolerance = MovementDecision.DefaultTolerance,
		float lastPointTolerance = MovementDecision.DefaultTolerance,
		bool fly = false,
		bool useMesh = true)
	{
		try
		{
			return MoveCore(position, tolerance, lastPointTolerance, fly, useMesh);
		}
		catch (Exception ex)
		{
			pluginLog.Debug($"MovementHelper.Move soft-fail: {ex.Message}");
			return false;
		}
	}

	/// <summary>
	/// Convenience: fly when mounted and zone supports flying; otherwise ground.
	/// </summary>
	public bool MovePreferFlyWhenMounted(
		Vector3 position,
		float tolerance = MovementDecision.DefaultTolerance,
		float lastPointTolerance = MovementDecision.DefaultTolerance,
		bool useMesh = true)
	{
		var fly = MovementDecision.PreferFlyWhenMounted(
			condition[ConditionFlag.Mounted],
			IsFlyingSupported());
		return Move(position, tolerance, lastPointTolerance, fly, useMesh);
	}

	private bool MoveCore(
		Vector3 position,
		float tolerance,
		float lastPointTolerance,
		bool fly,
		bool useMesh)
	{
		var player = objectTable.LocalPlayer;
		var playerValid = TeleportGate.IsPlayerReady(
			player != null,
			player is { CurrentHp: > 0 },
			condition[ConditionFlag.Unconscious]);

		var playerPos = player?.Position ?? Vector3.Zero;
		var distance = player != null
			? MovementDecision.Distance(playerPos, position)
			: float.MaxValue;

		var screenReady = TeleportGate.IsScreenReady(
			condition[ConditionFlag.BetweenAreas],
			condition[ConditionFlag.BetweenAreas51],
			condition[ConditionFlag.OccupiedInCutSceneEvent],
			condition[ConditionFlag.WatchingCutscene]);
		var playerReady = playerValid && screenReady;

		var navReady = vnav.NavIsReady();
		var pathfindInProgress = vnav.SimpleMovePathfindInProgress();
		var numWaypoints = vnav.PathNumWaypoints();
		var pathIsRunning = vnav.PathIsRunning();

		// Fresh budget once vnav is actually following a path (post-mesh-load success).
		if (MeshPathfindRetryDecision.ShouldResetOnNavProgress(pathIsRunning, numWaypoints))
		{
			MeshPathfindRetryDecision.Reset(ref nextMeshPathfindAttemptMs, ref meshPathfindAttempts);
			meshPathfindExhaustedLogged = false;
		}

		// After territory swap: do not pathfind until the player projects onto the new mesh.
		var playerOnMesh = !useMesh || !navReady || IsPlayerOnMesh(playerPos);

		var decision = MovementDecision.DecideMoveTick(
			playerValid,
			fly,
			IsFlyingSupported(),
			condition[ConditionFlag.Mounted],
			condition[ConditionFlag.InFlight],
			condition[ConditionFlag.Casting],
			position,
			distance,
			lastPointTolerance,
			useMesh,
			playerReady,
			navReady,
			pathfindInProgress,
			numWaypoints,
			pathIsRunning,
			playerOnMesh);

		switch (decision.Kind)
		{
			case MoveTickKind.WaitPlayerInvalid:
			case MoveTickKind.Wait:
				return false;
			case MoveTickKind.Arrived:
				if (decision.StopPath)
					vnav.PathStop();
				ResetMeshPathfindRetry();
				return true;
			case MoveTickKind.Takeoff:
				if (TryFireTakeoff())
					TryJumpTakeoff();
				return false;
			case MoveTickKind.SetLastPointToleranceAndWait:
				vnav.PathSetTolerance(lastPointTolerance);
				return false;
			case MoveTickKind.StartDirectPath:
				chat.TryExecuteCommand("/automove off");
				vnav.PathMoveTo(new List<Vector3> { position }, decision.Fly);
				return false;
			case MoveTickKind.StartMeshPath:
				return TryStartMeshPath(position, tolerance, decision.Fly);
			default:
				return false;
		}
	}

	private bool TryStartMeshPath(Vector3 position, float tolerance, bool fly)
	{
		var now = Environment.TickCount64;
		var retry = MeshPathfindRetryDecision.Decide(
			canStartMeshPathfind: true,
			now,
			nextMeshPathfindAttemptMs,
			meshPathfindAttempts);

		switch (retry)
		{
			case MeshPathfindRetryKind.WaitCooldown:
				return false;
			case MeshPathfindRetryKind.Exhausted:
				if (!meshPathfindExhaustedLogged)
				{
					meshPathfindExhaustedLogged = true;
					pluginLog.Debug(
						$"Mesh pathfind soft-retry exhausted after {meshPathfindAttempts} attempts");
				}

				return false;
			case MeshPathfindRetryKind.WaitNotReady:
				return false;
			case MeshPathfindRetryKind.Start:
			default:
				break;
		}

		chat.TryExecuteCommand("/automove off");
		vnav.PathSetTolerance(tolerance);
		_ = vnav.SimpleMovePathfindAndMoveTo(position, fly);
		// Throttle even on IPC true: silent poly→0 failures otherwise re-queue every tick.
		MeshPathfindRetryDecision.AfterStartAttempt(
			ref nextMeshPathfindAttemptMs,
			ref meshPathfindAttempts,
			now);
		return false;
	}

	/// <summary>
	/// True when the local player projects onto the loaded navmesh floor.
	/// Soft-fails false (wait) when mesh / IPC unavailable.
	/// </summary>
	private bool IsPlayerOnMesh(Vector3 playerPos)
	{
		try
		{
			var floor = vnav.QueryMeshPointOnFloor(
				playerPos,
				allowUnlandable: true,
				halfExtentXZ: FlagWorldPosition.DefaultHalfExtentXZ);
			return floor != null;
		}
		catch
		{
			return false;
		}
	}

	private bool TryFireTakeoff()
	{
		var now = Environment.TickCount64;
		if (now < nextTakeoffMs)
			return false;

		nextTakeoffMs = now + MovementDecision.TakeoffCooldownMs;
		return true;
	}

	private unsafe void TryJumpTakeoff()
	{
		try
		{
			var am = ActionManager.Instance();
			if (am == null)
				return;

			am->UseAction(ActionType.GeneralAction, MovementDecision.JumpGeneralActionId);
		}
		catch (Exception ex)
		{
			pluginLog.Debug($"Jump takeoff soft-fail: {ex.Message}");
		}
	}

	private static unsafe bool IsAetherCurrentZoneComplete(uint aetherCurrentCompFlgSetRowId)
	{
		try
		{
			var state = PlayerState.Instance();
			if (state == null)
				return false;

			return state->IsAetherCurrentZoneComplete(aetherCurrentCompFlgSetRowId);
		}
		catch
		{
			return false;
		}
	}
}
