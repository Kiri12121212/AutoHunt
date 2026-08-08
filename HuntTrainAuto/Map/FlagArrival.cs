#nullable enable
using System.Numerics;

namespace HuntTrainAuto.Map;

/// <summary>Result of <see cref="FlagArrival.Evaluate"/>.</summary>
public readonly struct FlagArrivalResult
{
	/// <summary>Player is within <see cref="Configuration.FlagArrivalTolerance"/> of flag <see cref="HuntFlag.WorldPos"/> (XZ).</summary>
	public required bool IsArrived { get; init; }

	/// <summary>
	/// Path should be stopped (<see cref="Contracts.IVnavmeshService.PathStop"/>) once on arrival.
	/// False after the stop has already been issued for this flag so follow/unmount nav can start.
	/// Does not dismount — see <see cref="UnmountRunner"/>.
	/// </summary>
	public required bool ShouldStopPath { get; init; }

	/// <summary>XZ distance to flag world pos; <see cref="float.MaxValue"/> when world pos missing/invalid.</summary>
	public float Distance { get; init; }
}

/// <summary>
/// Pure hunt-flag arrival decisions (TASKS 4.10 / brief 4.5).
/// Hunt flags are area markers — use <see cref="DefaultTolerance"/> (~5y) on <b>XZ</b>, not 3D
/// (flying above the floor PointOnFloor must still count as arrived so Unmount can dismount-land).
/// Unmount is owned by <see cref="UnmountRunner"/> — this only signals arrival / one-shot stop-path.
/// </summary>
public static class FlagArrival
{
	/// <summary>Default <see cref="Configuration.FlagArrivalTolerance"/> (yalms). TASKS example ~5y.</summary>
	public const float DefaultTolerance = 5f;

	/// <summary>
	/// Whether XZ distance to flag is within tolerance (mesh-style <see cref="MovementDecision.IsArrived"/>).
	/// Missing / zero <paramref name="flagWorldPos"/> → not arrived.
	/// <paramref name="inFlight"/> is accepted for call-site compatibility; XZ arrival applies either way.
	/// </summary>
	public static bool IsArrived(
		Vector3 playerPos,
		Vector3? flagWorldPos,
		float tolerance,
		bool inFlight = false)
	{
		_ = inFlight;
		if (flagWorldPos is not { } world || world == Vector3.Zero)
			return false;

		var distance = MovementDecision.DistanceXZ(playerPos, world);
		return MovementDecision.IsArrived(world, distance, tolerance, useMesh: true);
	}

	/// <summary>
	/// Whether vnavmesh path should stop this tick.
	/// One-shot: arrived and path not yet stopped for this arrival (avoids canceling follow nav).
	/// </summary>
	public static bool ShouldStopPath(bool isArrived, bool pathAlreadyStoppedForArrival)
		=> isArrived && !pathAlreadyStoppedForArrival;

	/// <summary>
	/// Full arrival evaluation for a Framework / nav tick.
	/// </summary>
	/// <param name="playerPos">Local player world position.</param>
	/// <param name="flagWorldPos"><see cref="HuntFlag.WorldPos"/>; null until PointOnFloor.</param>
	/// <param name="tolerance"><see cref="Configuration.FlagArrivalTolerance"/>.</param>
	/// <param name="pathAlreadyStoppedForArrival">True after PathStop was issued for the current flag.</param>
	/// <param name="inFlight">Unused for the distance check (XZ); kept for callers.</param>
	public static FlagArrivalResult Evaluate(
		Vector3 playerPos,
		Vector3? flagWorldPos,
		float tolerance,
		bool pathAlreadyStoppedForArrival = false,
		bool inFlight = false)
	{
		_ = inFlight;
		if (flagWorldPos is not { } world || world == Vector3.Zero)
		{
			return new FlagArrivalResult
			{
				IsArrived = false,
				ShouldStopPath = false,
				Distance = float.MaxValue,
			};
		}

		var distance = MovementDecision.DistanceXZ(playerPos, world);
		var arrived = MovementDecision.IsArrived(world, distance, tolerance, useMesh: true);
		return new FlagArrivalResult
		{
			IsArrived = arrived,
			ShouldStopPath = ShouldStopPath(arrived, pathAlreadyStoppedForArrival),
			Distance = distance,
		};
	}
}
