#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;

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
}
