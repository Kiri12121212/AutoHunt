#nullable enable
using System;
using System.Numerics;
using HuntTrainAuto.Services;

namespace HuntTrainAuto;

/// <summary>
/// Thin vnavmesh wiring: map-link / flag coords → floor <see cref="Vector3"/> via
/// <see cref="VNavmeshIpc.QueryMeshPointOnFloor"/>. Soft-fails (null); never throws to callers.
/// Pure math / fallback live in <see cref="FlagWorldPosition"/>.
/// </summary>
public sealed class FlagWorldHelper
{
	private readonly VNavmeshIpc vnav;

	public FlagWorldHelper(VNavmeshIpc vnav)
	{
		this.vnav = vnav ?? throw new ArgumentNullException(nameof(vnav));
	}

	/// <summary>
	/// Project <paramref name="flag"/> raw X/Y onto the navmesh floor and store on
	/// <see cref="HuntFlag.WorldPos"/>. Returns null when PointOnFloor misses (leave unset; retry later).
	/// </summary>
	public Vector3? TryResolve(HuntFlag flag)
	{
		ArgumentNullException.ThrowIfNull(flag);
		try
		{
			var query = FlagWorldPosition.PointOnFloorQueryFromRaw(flag.RawX, flag.RawY);
			var floor = vnav.QueryMeshPointOnFloor(
				query,
				FlagWorldPosition.DefaultAllowUnlandable,
				FlagWorldPosition.DefaultHalfExtentXZ);
			return FlagWorldPosition.Attach(flag, floor);
		}
		catch
		{
			return FlagWorldPosition.Attach(flag, null);
		}
	}

	/// <summary>
	/// Same as <see cref="TryResolve"/> but using AgentMap / world XZ floats.
	/// </summary>
	public Vector3? TryResolveFromWorldXZ(HuntFlag flag, float worldX, float worldZ)
	{
		ArgumentNullException.ThrowIfNull(flag);
		try
		{
			var query = FlagWorldPosition.PointOnFloorQueryFromWorldXZ(worldX, worldZ);
			var floor = vnav.QueryMeshPointOnFloor(
				query,
				FlagWorldPosition.DefaultAllowUnlandable,
				FlagWorldPosition.DefaultHalfExtentXZ);
			return FlagWorldPosition.Attach(flag, floor);
		}
		catch
		{
			return FlagWorldPosition.Attach(flag, null);
		}
	}
}
