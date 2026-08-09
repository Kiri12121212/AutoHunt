#nullable enable
using System;
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

	/// <summary>Stable debug description of the evaluated arrival outcome.</summary>
	public string Describe()
	{
		if (Distance == float.MaxValue)
			return "waiting: flag world position unavailable";
		if (ShouldStopPath)
			return $"arrived: stop path (distance {Distance:0.00})";
		return IsArrived
			? $"arrived: path already stopped (distance {Distance:0.00})"
			: $"moving: distance {Distance:0.00}";
	}
}

/// <summary>
/// Pure hunt-flag arrival decisions (TASKS 4.10 / brief 4.5).
/// Hunt flags are area markers — use <see cref="DefaultTolerance"/> (~5y) on <b>XZ</b>.
/// While <c>InFlight</c>, also require near-floor altitude so PathStop does not cancel descent
/// high above the PointOnFloor (Navigate would then never finish → no Unmount).
/// Unmount is owned by <see cref="UnmountRunner"/> — this only signals arrival / one-shot stop-path.
/// </summary>
public static class FlagArrival
{
	/// <summary>Default <see cref="Configuration.FlagArrivalTolerance"/> (yalms). TASKS example ~5y.</summary>
	public const float DefaultTolerance = 5f;

	/// <summary>
	/// Max |player.Y − floor.Y| while InFlight before PathStop + Unmount.
	/// Matches <see cref="MovementDecision.InFlightFloorTolerance"/> (live hover ~0.3–0.6y).
	/// </summary>
	public const float InFlightMaxVerticalDelta = MovementDecision.InFlightFloorTolerance;

	/// <summary>
	/// Whether XZ distance to flag is within tolerance (mesh-style <see cref="MovementDecision.IsArrived"/>).
	/// Missing / zero <paramref name="flagWorldPos"/> → not arrived.
	/// While <paramref name="inFlight"/>, also requires near-floor altitude.
	/// </summary>
	public static bool IsArrived(
		Vector3 playerPos,
		Vector3? flagWorldPos,
		float tolerance,
		bool inFlight = false)
	{
		if (flagWorldPos is not { } world || world == Vector3.Zero)
			return false;

		var distance = MovementDecision.DistanceXZ(playerPos, world);
		if (!MovementDecision.IsArrived(world, distance, tolerance, useMesh: true))
			return false;

		if (!inFlight)
			return true;

		// Still high above floor PointOnFloor — keep Navigate flying down.
		return MathF.Abs(playerPos.Y - world.Y) <= InFlightMaxVerticalDelta;
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
	/// <param name="inFlight">When true, require near-floor altitude as well as XZ.</param>
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

		var distance = MovementDecision.DistanceXZ(playerPos, world);
		var arrived = IsArrived(playerPos, world, tolerance, inFlight);
		return new FlagArrivalResult
		{
			IsArrived = arrived,
			ShouldStopPath = ShouldStopPath(arrived, pathAlreadyStoppedForArrival),
			Distance = distance,
		};
	}
}
