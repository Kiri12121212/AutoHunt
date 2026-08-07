#nullable enable
using System;
using System.Numerics;

namespace HuntTrainAuto;

/// <summary>
/// Pure map-link / flag → world helpers (AD <c>MapHelper</c> PointOnFloor prep).
/// Mesh projection stays in <see cref="FlagWorldHelper"/>.
/// </summary>
public static class FlagWorldPosition
{
	/// <summary>AD <c>MapHelper</c> query height for <c>PointOnFloor</c>.</summary>
	public const float PointOnFloorQueryY = 1024f;

	/// <summary>AD <c>MapHelper</c> half-extent for <c>PointOnFloor</c> (yalms).</summary>
	public const float DefaultHalfExtentXZ = 5f;

	/// <summary>AD <c>MapHelper</c> <c>allowUnlandable</c> for flag floor queries.</summary>
	public const bool DefaultAllowUnlandable = false;

	/// <summary>
	/// Map-link raw milli-units → world XZ (same units as AgentMap <c>XFloat</c>/<c>YFloat</c>).
	/// </summary>
	public static Vector2 WorldXZFromRaw(int rawX, int rawY)
		=> new(rawX / 1000f, rawY / 1000f);

	/// <summary>
	/// Approximate world position before floor projection (Y typically 0 until mesh hit).
	/// </summary>
	public static Vector3 ApproximateFromRaw(int rawX, int rawY, float y = 0f)
	{
		var xz = WorldXZFromRaw(rawX, rawY);
		return new Vector3(xz.X, y, xz.Y);
	}

	/// <summary>
	/// World query for vnavmesh <c>Query.Mesh.PointOnFloor</c>
	/// (AD: <c>new Vector3(XFloat, 1024, YFloat)</c>).
	/// </summary>
	public static Vector3 PointOnFloorQueryFromRaw(int rawX, int rawY)
		=> ApproximateFromRaw(rawX, rawY, PointOnFloorQueryY);

	/// <summary>
	/// World query from AgentMap / already-world XZ floats.
	/// </summary>
	public static Vector3 PointOnFloorQueryFromWorldXZ(float worldX, float worldZ)
		=> new(worldX, PointOnFloorQueryY, worldZ);

	/// <summary>
	/// Floor hit wins. When null (mesh missing / unloaded / no hit), return null —
	/// do not navigate on approximate Y=0 (caller should retry when nav is ready).
	/// </summary>
	public static Vector3? ChooseWorldPos(Vector3? floorHit)
		=> floorHit;

	/// <summary>
	/// Assigns <see cref="HuntFlag.WorldPos"/> from floor hit (clears when null).
	/// Does not pathfind or move.
	/// </summary>
	public static Vector3? Attach(HuntFlag flag, Vector3? floorHit)
	{
		ArgumentNullException.ThrowIfNull(flag);
		var world = ChooseWorldPos(floorHit);
		flag.WorldPos = world;
		return world;
	}
}
