#nullable enable
using System;
using System.Numerics;
using Dalamud.Plugin.Services;
using HuntTrainAuto.Logging;

namespace HuntTrainAuto.Movement;

/// <summary>
/// Thin vnavmesh wiring: map-link / flag coords → floor <see cref="Vector3"/> via
/// <see cref="IVnavmeshService.QueryMeshPointOnFloor"/>. Soft-fails (null); never throws to callers.
/// Pure math / fallback live in <see cref="FlagWorldPosition"/>.
/// </summary>
public sealed class FlagWorldHelper
{
	private readonly IVnavmeshService vnav;
	private readonly IPluginLog? pluginLog;

	public FlagWorldHelper(IVnavmeshService vnav, IPluginLog? pluginLog = null)
	{
		this.vnav = vnav ?? throw new ArgumentNullException(nameof(vnav));
		this.pluginLog = pluginLog;
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
			if (floor is null)
			{
				DebugBehavior.DebugThrottled(
					pluginLog!,
					enabled: true,
					throttleKey: "flagWorld.raw.floorMiss",
					intervalMs: 2000,
					nowMs: Environment.TickCount64,
					area: "Move",
					message: $"flag floor query missed raw=({flag.RawX:0.0},{flag.RawY:0.0})");
			}
			return FlagWorldPosition.Attach(flag, floor);
		}
		catch (Exception ex)
		{
			DebugBehavior.Debug(pluginLog!, enabled: true, "Move", $"flag floor query soft-fail: {ex.Message}");
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
			if (floor is null)
			{
				DebugBehavior.DebugThrottled(
					pluginLog!,
					enabled: true,
					throttleKey: "flagWorld.world.floorMiss",
					intervalMs: 2000,
					nowMs: Environment.TickCount64,
					area: "Move",
					message: $"flag floor query missed world=({worldX:0.0},{worldZ:0.0})");
			}
			return FlagWorldPosition.Attach(flag, floor);
		}
		catch (Exception ex)
		{
			DebugBehavior.Debug(pluginLog!, enabled: true, "Move", $"flag floor query soft-fail: {ex.Message}");
			return FlagWorldPosition.Attach(flag, null);
		}
	}
}
