#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using HuntTrainAuto.Contracts;

namespace HuntTrainAuto.Teleport;

/// <summary>
/// Soft-fail sampler: kick off / resolve vnav path lengths for same-zone time-aware TP.
/// No unit tests against live IPC — pure length math lives in <see cref="SameZoneTravelCost"/>.
/// </summary>
public static class SameZonePathCostSampler
{
	/// <summary>Compact, side-effect-free sampler diagnostic for call-site logging.</summary>
	public static string Describe(SameZoneTravelEstimate estimate)
		=> $"path sampler {estimate.Describe()}";

	/// <summary>Soft-timeout before treating pending pathfind as unavailable.</summary>
	public const int DefaultTimeoutMs = 3000;

	/// <summary>
	/// Start player→flag and aetheryte→flag pathfinds when nav is ready.
	/// Returns false when vnav/endpoints unavailable (caller uses distance fallback).
	/// </summary>
	public static bool TryBegin(
		IVnavmeshService vnav,
		Vector3 playerWorld,
		Vector3 flagFloor,
		Vector3 aetheryteFloor,
		bool canFly,
		out SameZoneTravelEstimate estimate,
		out Task<List<Vector3>>? directTask,
		out Task<List<Vector3>>? aetheryteTask)
	{
		estimate = Unavailable();
		directTask = null;
		aetheryteTask = null;

		ArgumentNullException.ThrowIfNull(vnav);
		try
		{
			if (!vnav.IsAvailable || !vnav.NavIsReady())
				return false;

			directTask = vnav.NavPathfind(playerWorld, flagFloor, canFly);
			aetheryteTask = vnav.NavPathfind(aetheryteFloor, flagFloor, canFly);
			if (directTask == null || aetheryteTask == null)
			{
				directTask = null;
				aetheryteTask = null;
				return false;
			}

			if (TryResolve(directTask, aetheryteTask, out estimate))
				return true;

			estimate = Pending();
			return true;
		}
		catch
		{
			estimate = Unavailable();
			directTask = null;
			aetheryteTask = null;
			return false;
		}
	}

	/// <summary>
	/// Resolve completed tasks into Ready / Unavailable. False when still running.
	/// </summary>
	public static bool TryResolve(
		Task<List<Vector3>> directTask,
		Task<List<Vector3>> aetheryteTask,
		out SameZoneTravelEstimate estimate)
	{
		ArgumentNullException.ThrowIfNull(directTask);
		ArgumentNullException.ThrowIfNull(aetheryteTask);

		if (!directTask.IsCompleted || !aetheryteTask.IsCompleted)
		{
			estimate = Pending();
			return false;
		}

		if (!directTask.IsCompletedSuccessfully || !aetheryteTask.IsCompletedSuccessfully)
		{
			estimate = Unavailable();
			return true;
		}

		List<Vector3> direct;
		List<Vector3> fromAeth;
		try
		{
			direct = directTask.Result;
			fromAeth = aetheryteTask.Result;
		}
		catch
		{
			estimate = Unavailable();
			return true;
		}

		if (direct == null || fromAeth == null || direct.Count == 0 || fromAeth.Count == 0)
		{
			estimate = Unavailable();
			return true;
		}

		estimate = new SameZoneTravelEstimate
		{
			Status = SameZonePathCostStatus.Ready,
			DirectPathLengthYalms = SameZoneTravelCost.PathLength(direct),
			AetherytePathLengthYalms = SameZoneTravelCost.PathLength(fromAeth),
		};
		return true;
	}

	public static SameZoneTravelEstimate Pending() => new()
	{
		Status = SameZonePathCostStatus.Pending,
	};

	public static SameZoneTravelEstimate Unavailable() => new()
	{
		Status = SameZonePathCostStatus.Unavailable,
	};
}
