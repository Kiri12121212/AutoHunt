#nullable enable
using System;
using System.Numerics;
using HuntTrainAuto.Services;

namespace HuntTrainAuto;

/// <summary>
/// Thin vnavmesh wiring for flag-area arrival: evaluate + one-shot <see cref="VNavmeshIpc.PathStop"/>.
/// Soft-fails; never throws to callers. Does not dismount — see <see cref="UnmountRunner"/>.
/// </summary>
public sealed class FlagArrivalHelper
{
	private readonly VNavmeshIpc vnav;
	private bool pathStoppedForArrival;
	private Vector3? latchedWorldPos;

	public FlagArrivalHelper(VNavmeshIpc vnav)
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
	public FlagArrivalResult Tick(Vector3 playerPos, Vector3? flagWorldPos, float tolerance)
	{
		try
		{
			if (!SameWorldPos(flagWorldPos, latchedWorldPos))
			{
				pathStoppedForArrival = false;
				latchedWorldPos = flagWorldPos;
			}

			var result = FlagArrival.Evaluate(playerPos, flagWorldPos, tolerance, pathStoppedForArrival);
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
