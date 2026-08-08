#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace HuntTrainAuto.Contracts;

/// <summary>vnavmesh plugin IPC surface.</summary>
public interface IVnavmeshService : IDisposable
{
	bool IsAvailable { get; }

	bool NavIsReady();

	void PathStop();

	bool PathIsRunning();

	int PathNumWaypoints();

	void PathMoveTo(List<Vector3> waypoints, bool fly);

	bool SimpleMovePathfindAndMoveTo(Vector3 destination, bool fly);

	bool SimpleMovePathfindInProgress();

	void PathSetTolerance(float tolerance);

	Vector3? QueryMeshPointOnFloor(Vector3 position, bool allowUnlandable, float halfExtentXZ);

	/// <summary>
	/// Async mesh pathfind (<c>vnavmesh.Nav.Pathfind</c>). Soft-fails (null) when unavailable.
	/// Does not move the character.
	/// </summary>
	Task<List<Vector3>>? NavPathfind(Vector3 from, Vector3 to, bool fly);
}
