#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using HuntTrainAuto.Contracts;

namespace HuntTrainAuto.Services;

/// <summary>
/// Soft-fail wrapper around the vnavmesh plugin IPC
/// (<see href="https://github.com/awgil/ffxiv_navmesh">awgil/ffxiv_navmesh</see>).
/// Channels are <c>vnavmesh.{Category}.{Operation}</c> as registered by
/// <c>IPCProvider</c>. Cannot be unit-tested without a live Dalamud + vnavmesh
/// provider — probe/availability is CallGate try/catch only.
/// </summary>
public sealed class VNavmeshIpc : IVnavmeshService
{
	/// <summary>IPC: <c>vnavmesh.Nav.IsReady</c> — <c>Func&lt;bool&gt;</c>.</summary>
	private const string NavIsReadyChannel = "vnavmesh.Nav.IsReady";

	/// <summary>IPC: <c>vnavmesh.Path.Stop</c> — <c>Action</c>.</summary>
	private const string PathStopChannel = "vnavmesh.Path.Stop";

	/// <summary>IPC: <c>vnavmesh.Path.IsRunning</c> — <c>Func&lt;bool&gt;</c>.</summary>
	private const string PathIsRunningChannel = "vnavmesh.Path.IsRunning";

	/// <summary>IPC: <c>vnavmesh.Path.NumWaypoints</c> — <c>Func&lt;int&gt;</c>.</summary>
	private const string PathNumWaypointsChannel = "vnavmesh.Path.NumWaypoints";

	/// <summary>IPC: <c>vnavmesh.Path.SetTolerance</c> — <c>Action&lt;float&gt;</c>.</summary>
	private const string PathSetToleranceChannel = "vnavmesh.Path.SetTolerance";

	/// <summary>
	/// IPC: <c>vnavmesh.Path.MoveTo</c> —
	/// <c>Action&lt;List&lt;Vector3&gt;, bool&gt;</c>.
	/// </summary>
	private const string PathMoveToChannel = "vnavmesh.Path.MoveTo";

	/// <summary>
	/// IPC: <c>vnavmesh.SimpleMove.PathfindAndMoveTo</c> —
	/// <c>Func&lt;Vector3, bool, bool&gt;</c>.
	/// </summary>
	private const string PathfindAndMoveToChannel = "vnavmesh.SimpleMove.PathfindAndMoveTo";

	/// <summary>
	/// IPC: <c>vnavmesh.SimpleMove.PathfindInProgress</c> — <c>Func&lt;bool&gt;</c>.
	/// </summary>
	private const string SimpleMovePathfindInProgressChannel = "vnavmesh.SimpleMove.PathfindInProgress";

	/// <summary>
	/// IPC: <c>vnavmesh.Query.Mesh.PointOnFloor</c> —
	/// <c>Func&lt;Vector3, bool, float, Vector3?&gt;</c>.
	/// </summary>
	private const string PointOnFloorChannel = "vnavmesh.Query.Mesh.PointOnFloor";

	private readonly ICallGateSubscriber<bool> navIsReady;
	private readonly ICallGateSubscriber<object?> pathStop;
	private readonly ICallGateSubscriber<bool> pathIsRunning;
	private readonly ICallGateSubscriber<int> pathNumWaypoints;
	private readonly ICallGateSubscriber<float, object> pathSetTolerance;
	private readonly ICallGateSubscriber<List<Vector3>, bool, object> pathMoveTo;
	private readonly ICallGateSubscriber<Vector3, bool, bool> pathfindAndMoveTo;
	private readonly ICallGateSubscriber<bool> simpleMovePathfindInProgress;
	private readonly ICallGateSubscriber<Vector3, bool, float, Vector3?> pointOnFloor;

	public VNavmeshIpc(IDalamudPluginInterface pluginInterface)
	{
		navIsReady = pluginInterface.GetIpcSubscriber<bool>(NavIsReadyChannel);
		pathStop = pluginInterface.GetIpcSubscriber<object?>(PathStopChannel);
		pathIsRunning = pluginInterface.GetIpcSubscriber<bool>(PathIsRunningChannel);
		pathNumWaypoints = pluginInterface.GetIpcSubscriber<int>(PathNumWaypointsChannel);
		pathSetTolerance = pluginInterface.GetIpcSubscriber<float, object>(PathSetToleranceChannel);
		pathMoveTo = pluginInterface.GetIpcSubscriber<List<Vector3>, bool, object>(PathMoveToChannel);
		pathfindAndMoveTo = pluginInterface.GetIpcSubscriber<Vector3, bool, bool>(PathfindAndMoveToChannel);
		simpleMovePathfindInProgress = pluginInterface.GetIpcSubscriber<bool>(SimpleMovePathfindInProgressChannel);
		pointOnFloor = pluginInterface.GetIpcSubscriber<Vector3, bool, float, Vector3?>(PointOnFloorChannel);
	}
	/// <summary>
	/// True when the <c>vnavmesh.Nav.IsReady</c> CallGate has a registered provider.
	/// Mesh may still be building (<see cref="NavIsReady"/> false) — this only
	/// proves IPC is reachable. Soft-fails: never throws to callers.
	/// </summary>
	public bool IsAvailable
	{
		get
		{
			try
			{
				_ = navIsReady.InvokeFunc();
				return true;
			}
			catch
			{
				return false;
			}
		}
	}

	/// <summary>
	/// Whether the navmesh is loaded and ready (<c>vnavmesh.Nav.IsReady</c>).
	/// Soft-fails (returns false) when vnavmesh is absent or IPC throws.
	/// </summary>
	public bool NavIsReady()
	{
		try
		{
			return navIsReady.InvokeFunc();
		}
		catch
		{
			return false;
		}
	}

	/// <summary>
	/// Stop active path following (<c>vnavmesh.Path.Stop</c>). Soft-fails silently.
	/// </summary>
	public void PathStop()
	{
		try
		{
			pathStop.InvokeAction();
		}
		catch
		{
			// vnavmesh may be absent.
		}
	}

	/// <summary>
	/// Whether a path is currently being followed (<c>vnavmesh.Path.IsRunning</c>).
	/// Soft-fails (returns false) when vnavmesh is absent or IPC throws.
	/// </summary>
	public bool PathIsRunning()
	{
		try
		{
			return pathIsRunning.InvokeFunc();
		}
		catch
		{
			return false;
		}
	}

	/// <summary>
	/// Remaining path waypoints (<c>vnavmesh.Path.NumWaypoints</c>).
	/// Soft-fails (returns 0) when vnavmesh is absent or IPC throws.
	/// </summary>
	public int PathNumWaypoints()
	{
		try
		{
			return pathNumWaypoints.InvokeFunc();
		}
		catch
		{
			return 0;
		}
	}

	/// <summary>
	/// Follow an explicit waypoint list (<c>vnavmesh.Path.MoveTo</c>).
	/// Soft-fails silently when vnavmesh is absent or IPC throws.
	/// </summary>
	/// <param name="waypoints">World waypoints.</param>
	/// <param name="fly">True to follow in flight mode.</param>
	public void PathMoveTo(List<Vector3> waypoints, bool fly)
	{
		try
		{
			pathMoveTo.InvokeAction(waypoints, fly);
		}
		catch
		{
			// vnavmesh may be absent.
		}
	}

	/// <summary>
	/// Pathfind then follow to <paramref name="destination"/>
	/// (<c>vnavmesh.SimpleMove.PathfindAndMoveTo</c>).
	/// Soft-fails (returns false) when vnavmesh is absent, busy, or IPC throws.
	/// </summary>
	/// <param name="destination">World destination.</param>
	/// <param name="fly">True to use flying pathfinding / movement.</param>
	public bool SimpleMovePathfindAndMoveTo(Vector3 destination, bool fly)
	{
		try
		{
			return pathfindAndMoveTo.InvokeFunc(destination, fly);
		}
		catch
		{
			return false;
		}
	}

	/// <summary>
	/// Whether a SimpleMove pathfind task is in progress
	/// (<c>vnavmesh.SimpleMove.PathfindInProgress</c>).
	/// Soft-fails (returns false) when vnavmesh is absent or IPC throws.
	/// </summary>
	public bool SimpleMovePathfindInProgress()
	{
		try
		{
			return simpleMovePathfindInProgress.InvokeFunc();
		}
		catch
		{
			return false;
		}
	}

	/// <summary>
	/// Set path-follow arrival tolerance in yalms (<c>vnavmesh.Path.SetTolerance</c>).
	/// Soft-fails silently.
	/// </summary>
	public void PathSetTolerance(float tolerance)
	{
		try
		{
			pathSetTolerance.InvokeAction(tolerance);
		}
		catch
		{
			// vnavmesh may be absent.
		}
	}

	/// <summary>
	/// Project <paramref name="position"/> onto the navmesh floor
	/// (<c>vnavmesh.Query.Mesh.PointOnFloor</c>).
	/// Soft-fails (returns null) when vnavmesh is absent, mesh unloaded, or IPC throws.
	/// </summary>
	/// <param name="position">World query position.</param>
	/// <param name="allowUnlandable">Pass through to vnavmesh (AD <c>a</c>).</param>
	/// <param name="halfExtentXZ">XZ search half-extent (AD <c>b</c>).</param>
	public Vector3? QueryMeshPointOnFloor(Vector3 position, bool allowUnlandable, float halfExtentXZ)
	{
		try
		{
			return pointOnFloor.InvokeFunc(position, allowUnlandable, halfExtentXZ);
		}
		catch
		{
			return null;
		}
	}

	public void Dispose()
	{
		// Subscriber only — no event subscriptions to tear down.
	}
}
