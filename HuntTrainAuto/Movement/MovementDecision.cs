#nullable enable
using System;
using System.Numerics;

namespace HuntTrainAuto.Movement;

/// <summary>Framework / Move tick outcome for AD <c>MovementHelper.Move</c>.</summary>
public enum MoveTickKind
{
	/// <summary>Player invalid — soft-wait (<c>Move</c> returns false).</summary>
	WaitPlayerInvalid,

	/// <summary>Within last-point tolerance (or zero destination) — stop path when applicable.</summary>
	Arrived,

	/// <summary>Need GeneralAction Jump (2) to enter flight.</summary>
	Takeoff,

	/// <summary>One waypoint left — set last-point tolerance, keep waiting.</summary>
	SetLastPointToleranceAndWait,

	/// <summary>Nav not ready, pathfind in progress, already pathing, or direct-move already running.</summary>
	Wait,

	/// <summary>Disable <c>/automove</c>, set path tolerance, <c>SimpleMove.PathfindAndMoveTo</c>.</summary>
	StartMeshPath,

	/// <summary>Disable <c>/automove</c>, <c>Path.MoveTo</c> (no mesh pathfind).</summary>
	StartDirectPath,
}

/// <summary>Reason a <see cref="MoveTickResult"/> is waiting.</summary>
public enum MoveWaitReason
{
	None,
	PlayerInvalid,
	TakeoffCasting,
	DirectPathRunning,
	MeshNotReady,
}

/// <summary>Result of <see cref="MovementDecision.DecideMoveTick"/>.</summary>
public readonly struct MoveTickResult
{
	public required MoveTickKind Kind { get; init; }

	/// <summary>Why <see cref="Kind"/> is a wait outcome.</summary>
	public MoveWaitReason WaitReason { get; init; }

	/// <summary>Effective fly flag after zone clamp (for Start* / Takeoff).</summary>
	public bool Fly { get; init; }

	/// <summary>True when <see cref="Kind"/> is <see cref="MoveTickKind.Arrived"/> and path should be stopped.</summary>
	public bool StopPath { get; init; }
}

/// <summary>
/// Pure movement / fly decisions (AD <c>MovementHelper</c>).
/// Territory / IPC / ActionManager wiring stays in <see cref="MovementHelper"/>.
/// </summary>
public static class MovementDecision
{
	/// <summary>AD default path / last-point tolerance (yalms).</summary>
	public const float DefaultTolerance = 0.25f;

	/// <summary>
	/// Arrival / PathStop / Unmount while still <c>InFlight</c> near the floor PointOnFloor.
	/// Wider than <see cref="DefaultTolerance"/> — live hover often sits ~0.3–0.6y above the dest
	/// and never clears <c>InFlight</c> without a dismount. 3y leaves margin for vnav hover.
	/// </summary>
	public const float InFlightFloorTolerance = 3f;

	/// <summary>GeneralAction id for Jump (flight takeoff).</summary>
	public const uint JumpGeneralActionId = 2;

	/// <summary>Human-readable decision outcome for movement debug logs.</summary>
	public static string Describe(MoveTickResult result)
		=> result.Kind switch
		{
			MoveTickKind.WaitPlayerInvalid => "wait (player invalid)",
			MoveTickKind.Arrived => result.StopPath ? "arrived (stop path)" : "arrived",
			MoveTickKind.Takeoff => "takeoff",
			MoveTickKind.SetLastPointToleranceAndWait => "wait (set last-point tolerance)",
			MoveTickKind.Wait => $"wait ({result.WaitReason})",
			MoveTickKind.StartMeshPath => result.Fly ? "start mesh path (fly)" : "start mesh path (ground)",
			MoveTickKind.StartDirectPath => result.Fly ? "start direct path (fly)" : "start direct path (ground)",
			_ => $"unknown ({result.Kind})",
		};

	/// <summary>
	/// AD <c>TerritoryIntendedUse</c> RowIds that can support flying (open world / related).
	/// </summary>
	public static bool IsFlyingTerritoryUse(uint territoryIntendedUseRowId)
		=> territoryIntendedUseRowId is 1 or 49 or 47;

	/// <summary>
	/// Zone can fly: non-zero territory, intended-use 1/49/47, and aether currents complete.
	/// </summary>
	public static bool ZoneSupportsFlying(
		uint territoryTypeId,
		uint territoryIntendedUseRowId,
		bool aetherCurrentZoneComplete)
		=> territoryTypeId != 0
			&& IsFlyingTerritoryUse(territoryIntendedUseRowId)
			&& aetherCurrentZoneComplete;

	/// <summary>If fly requested but zone does not support → force ground.</summary>
	public static bool ResolveFly(bool flyRequested, bool zoneSupportsFlying)
		=> flyRequested && zoneSupportsFlying;

	/// <summary>Prefer <c>canFly: true</c> when mounted in a flying-supported zone.</summary>
	public static bool PreferFlyWhenMounted(bool mounted, bool zoneSupportsFlying)
		=> mounted && zoneSupportsFlying;

	/// <summary>
	/// AD arrival: zero destination, or distance (minus 1 yalm when <paramref name="useMesh"/> is false)
	/// within <paramref name="lastPointTolerance"/>.
	/// </summary>
	public static bool IsArrived(
		Vector3 destination,
		float distanceToDestination,
		float lastPointTolerance,
		bool useMesh)
	{
		if (destination == Vector3.Zero)
			return true;

		var adjusted = distanceToDestination - (useMesh ? 0f : 1f);
		return adjusted <= lastPointTolerance;
	}

	/// <summary>Euclidean distance helper for arrival checks.</summary>
	public static float Distance(Vector3 from, Vector3 to)
		=> Vector3.Distance(from, to);

	/// <summary>Horizontal (XZ) distance — hunt flags are area markers; Y differs while flying.</summary>
	public static float DistanceXZ(Vector3 from, Vector3 to)
	{
		var dx = from.X - to.X;
		var dz = from.Z - to.Z;
		return MathF.Sqrt((dx * dx) + (dz * dz));
	}

	/// <summary>
	/// Need Jump takeoff when flying path requested, mounted, and not yet in flight.
	/// Unmounted callers must not spam Jump (does not enable flight on foot).
	/// </summary>
	public static bool NeedsTakeoff(bool fly, bool mounted, bool inFlight, bool casting)
		=> fly && mounted && !inFlight && !casting;

	/// <summary>Throttle Jump takeoff attempts (avoid per-tick spam).</summary>
	public const int TakeoffCooldownMs = 500;

	/// <summary>AD: set last-point tolerance when a single waypoint remains.</summary>
	public static bool ShouldSetLastPointTolerance(int numWaypoints)
		=> numWaypoints == 1;

	/// <summary>
	/// Max |player.Y − PointOnFloor.Y| to treat the player as on-mesh for ground pathfind.
	/// Larger gaps mean mid-air / falling (poly→0 spam after zone load).
	/// </summary>
	public const float PlayerOnMeshMaxYDelta = 6f;

	/// <summary>
	/// Soft-wait before starting a mesh pathfind (AD ready / nav / in-progress / waypoints / running guards).
	/// <paramref name="playerOnMesh"/>: after territory swap, wait until the local player projects
	/// onto the loaded mesh (avoids <c>poly → 0</c> spam while still falling / off-mesh).
	/// Ignored when <paramref name="fly"/> — voxel fly pathfind starts from mid-air; blocking on
	/// ground PointOnFloor stalls descent to the flag floor and prevents unmount.
	/// </summary>
	public static bool ShouldWaitBeforeMeshPathfind(
		bool playerReady,
		bool navReady,
		bool pathfindInProgress,
		int numWaypoints,
		bool pathIsRunning = false,
		bool playerOnMesh = true,
		bool fly = false)
		=> !playerReady
			|| !navReady
			|| (!fly && !playerOnMesh)
			|| pathfindInProgress
			|| numWaypoints > 0
			|| pathIsRunning;

	/// <summary>Inverse of <see cref="ShouldWaitBeforeMeshPathfind"/> — safe to start pathfind.</summary>
	public static bool CanStartMeshPathfind(
		bool playerReady,
		bool navReady,
		bool pathfindInProgress,
		int numWaypoints,
		bool pathIsRunning = false,
		bool playerOnMesh = true,
		bool fly = false)
		=> !ShouldWaitBeforeMeshPathfind(
			playerReady, navReady, pathfindInProgress, numWaypoints, pathIsRunning, playerOnMesh, fly);

	/// <summary>
	/// While still <c>InFlight</c>, only treat as arrived when within <see cref="InFlightFloorTolerance"/>
	/// so a loose flag last-point tolerance cannot PathStop high above the floor.
	/// </summary>
	public static bool IsArrivedForMove(
		Vector3 destination,
		float distanceToDestination,
		float lastPointTolerance,
		bool useMesh,
		bool inFlight)
	{
		if (!IsArrived(destination, distanceToDestination, lastPointTolerance, useMesh))
			return false;

		if (!inFlight)
			return true;

		return IsArrived(destination, distanceToDestination, InFlightFloorTolerance, useMesh);
	}

	/// <summary>
	/// One <c>Move</c> decision step after zone/fly resolution.
	/// Does not auto-mount (MountRunner owns that).
	/// </summary>
	public static MoveTickResult DecideMoveTick(
		bool playerValid,
		bool flyRequested,
		bool zoneSupportsFlying,
		bool mounted,
		bool inFlight,
		bool casting,
		Vector3 destination,
		float distanceToDestination,
		float lastPointTolerance,
		bool useMesh,
		bool playerReady,
		bool navReady,
		bool pathfindInProgress,
		int numWaypoints,
		bool pathIsRunning,
		bool playerOnMesh = true)
	{
		if (!playerValid)
		{
			return new MoveTickResult
			{
				Kind = MoveTickKind.WaitPlayerInvalid,
				Fly = false,
				WaitReason = MoveWaitReason.PlayerInvalid,
			};
		}

		var fly = ResolveFly(flyRequested, zoneSupportsFlying);
		// MountRunner owns mounting; unmounted fly requests path on the ground.
		if (fly && !mounted && !inFlight)
			fly = false;

		if (NeedsTakeoff(fly, mounted, inFlight, casting))
		{
			return new MoveTickResult
			{
				Kind = MoveTickKind.Takeoff,
				Fly = fly,
			};
		}

		// Casting while mounted and needing takeoff → wait (NeedsTakeoff false when casting).
		if (fly && mounted && !inFlight)
		{
			return new MoveTickResult
			{
				Kind = MoveTickKind.Wait,
				Fly = fly,
				WaitReason = MoveWaitReason.TakeoffCasting,
			};
		}

		if (IsArrivedForMove(
			    destination, distanceToDestination, lastPointTolerance, useMesh, inFlight))
		{
			return new MoveTickResult
			{
				Kind = MoveTickKind.Arrived,
				Fly = fly,
				StopPath = destination != Vector3.Zero,
			};
		}

		if (!useMesh)
		{
			if (pathIsRunning)
			{
				return new MoveTickResult
				{
					Kind = MoveTickKind.Wait,
					Fly = fly,
					WaitReason = MoveWaitReason.DirectPathRunning,
				};
			}

			return new MoveTickResult
			{
				Kind = MoveTickKind.StartDirectPath,
				Fly = fly,
			};
		}

		if (ShouldSetLastPointTolerance(numWaypoints))
		{
			return new MoveTickResult
			{
				Kind = MoveTickKind.SetLastPointToleranceAndWait,
				Fly = fly,
			};
		}

		if (ShouldWaitBeforeMeshPathfind(
			    playerReady,
			    navReady,
			    pathfindInProgress,
			    numWaypoints,
			    pathIsRunning,
			    playerOnMesh,
			    fly))
		{
			return new MoveTickResult
			{
				Kind = MoveTickKind.Wait,
				Fly = fly,
				WaitReason = MoveWaitReason.MeshNotReady,
			};
		}

		return new MoveTickResult
		{
			Kind = MoveTickKind.StartMeshPath,
			Fly = fly,
		};
	}

	public static string Describe(MoveTickResult result)
		=> result.Kind switch
		{
			MoveTickKind.WaitPlayerInvalid => "wait (player invalid)",
			MoveTickKind.Arrived => result.StopPath ? "arrived (stop path)" : "arrived",
			MoveTickKind.Takeoff => "takeoff",
			MoveTickKind.SetLastPointToleranceAndWait => "set last-point tolerance",
			MoveTickKind.Wait => "wait",
			MoveTickKind.StartMeshPath => $"start mesh path (fly={result.Fly})",
			MoveTickKind.StartDirectPath => $"start direct path (fly={result.Fly})",
			_ => $"unknown ({result.Kind})",
		};

}
