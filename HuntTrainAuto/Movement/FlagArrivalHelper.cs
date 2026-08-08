#nullable enable
using System;
using System.Numerics;

namespace HuntTrainAuto.Movement;

/// <summary>
/// Thin vnavmesh wiring for flag-area arrival: evaluate + one-shot <see cref="IVnavmeshService.PathStop"/>.
/// Soft-fails; never throws to callers. Does not dismount — see <see cref="UnmountRunner"/>.
/// </summary>
public sealed class FlagArrivalHelper
{
	private readonly IVnavmeshService vnav;
	private bool pathStoppedForArrival;
	private Vector3? latchedWorldPos;

	public FlagArrivalHelper(IVnavmeshService vnav)
	{
		this.vnav = vnav ?? throw new ArgumentNullException(nameof(vnav));
	}

	/// <summary>True after PathStop was issued for the current latched flag world pos.</summary>
	public bool PathStoppedForArrival => pathStoppedForArrival;

	/// <summary>Reset latch (new flag / abort). Call when hunt flag changes.</summary>
	public void Clear()
	{
		pathStoppedForArrival = false;
		latchedWorldPos = null;
	}

	/// <summary>
	/// Evaluate arrival; PathStop once when entering the tolerance radius.
	/// Subsequent ticks while still arrived report <see cref="FlagArrivalResult.IsArrived"/>
	/// but do not stop again (so unmount/follow can path).
	/// </summary>
	/// <param name="inFlight">Near-floor gate while flying (see <see cref="FlagArrival.IsArrived"/>).</param>
	public FlagArrivalResult Tick(
		Vector3 playerPos,
		Vector3? flagWorldPos,
		float tolerance,
		bool inFlight = false)
	{
		try
		{
			if (!SameWorldPos(flagWorldPos, latchedWorldPos))
			{
				pathStoppedForArrival = false;
				latchedWorldPos = flagWorldPos;
			}

			var result = FlagArrival.Evaluate(
				playerPos,
				flagWorldPos,
				tolerance,
				pathStoppedForArrival,
				inFlight);
			if (result.ShouldStopPath)
			{
				vnav.PathStop();
				pathStoppedForArrival = true;
			}

			return result;
		}
		catch
		{
			return new FlagArrivalResult
			{
				IsArrived = false,
				ShouldStopPath = false,
				Distance = float.MaxValue,
			};
		}
	}

	private static bool SameWorldPos(Vector3? a, Vector3? b)
	{
		if (a is null && b is null)
			return true;
		if (a is null || b is null)
			return false;
		return a.Value == b.Value;
	}
}
