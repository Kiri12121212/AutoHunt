#nullable enable
using System.Numerics;

namespace HuntTrainAuto.Map;

/// <summary>Result of <see cref="FlagArrival.Evaluate"/>.</summary>
public readonly struct FlagArrivalResult
{
	/// <summary>Player is within <see cref="Configuration.FlagArrivalTolerance"/> of flag <see cref="HuntFlag.WorldPos"/>.</summary>
	public required bool IsArrived { get; init; }

	/// <summary>
	/// Path should be stopped (<see cref="Contracts.IVnavmeshService.PathStop"/>) once on arrival.
	/// False after the stop has already been issued for this flag so follow/unmount nav can start.
	/// Does not dismount — see <see cref="UnmountRunner"/>.
	/// </summary>
	public required bool ShouldStopPath { get; init; }

	/// <summary>Euclidean distance to flag world pos; <see cref="float.MaxValue"/> when world pos missing/invalid.</summary>
	public float Distance { get; init; }
}

/// <summary>
/// Pure hunt-flag arrival decisions (TASKS 4.10 / brief 4.5).
/// Hunt flags are area markers — use <see cref="DefaultTolerance"/> (~5y), not AD's 0.25 path tolerance.
/// Reuses <see cref="MovementDecision.IsArrived"/> (mesh-style; no direct-path −1 yalm fudge).
/// Unmount is owned by <see cref="UnmountRunner"/> — this only signals arrival / one-shot stop-path.
/// </summary>
public static class FlagArrival
{
	/// <summary>Default <see cref="Configuration.FlagArrivalTolerance"/> (yalms). TASKS example ~5y.</summary>
	public const float DefaultTolerance = 5f;

	/// <summary>
	/// Whether distance to flag is within tolerance (mesh-style <see cref="MovementDecision.IsArrived"/>).
	/// Missing / zero <paramref name="flagWorldPos"/> → not arrived.
	/// While <paramref name="inFlight"/>, not arrived — keep descending onto the floor before unmount.
	/// </summary>
	public static bool IsArrived(
		Vector3 playerPos,
		Vector3? flagWorldPos,
		float tolerance,
		bool inFlight = false)
	{
		if (inFlight)
			return false;

		if (flagWorldPos is not { } world || world == Vector3.Zero)
			return false;

		var distance = MovementDecision.Distance(playerPos, world);
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
	/// <param name="inFlight">True while airborne — defer arrival until on the floor.</param>
	public static FlagArrivalResult Evaluate(
		Vector3 playerPos,
		Vector3? flagWorldPos,
		float tolerance,
		bool pathAlreadyStoppedForArrival = false,
		bool inFlight = false)
	{
		if (flagWorldPos is not { } world || world == Vector3.Zero)
		{
			return new FlagArrivalResult
			{
				IsArrived = false,
				ShouldStopPath = false,
				Distance = float.MaxValue,
			};
		}

		var distance = MovementDecision.Distance(playerPos, world);
		var arrived = !inFlight
			&& MovementDecision.IsArrived(world, distance, tolerance, useMesh: true);
		return new FlagArrivalResult
		{
			IsArrived = arrived,
			ShouldStopPath = ShouldStopPath(arrived, pathAlreadyStoppedForArrival),
			Distance = distance,
		};
	}
}
