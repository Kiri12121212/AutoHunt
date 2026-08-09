#nullable enable
using System;
using System.Numerics;
using HuntTrainAuto.Domain;
using HuntTrainAuto.Map;

namespace HuntTrainAuto.Combat;

/// <summary>Where the last engage position hint came from.</summary>
public enum EngagePositionHintSource
{
	None = 0,
	/// <summary>Conductor chat map-link flag.</summary>
	ConductorFlag = 1,
	/// <summary>HuntAlerts IPC mapped flag (already supplies train coords).</summary>
	HuntAlerts = 2,
	/// <summary>
	/// Soft Sonar chat map-link (sender "Sonar"). Sonar is a soft dependency —
	/// no public Dalamud IPC; chat intake only. Absent Sonar → no-op.
	/// </summary>
	SonarChat = 3,
}

/// <summary>
/// Last known hunt WorldPos used to bias NearbyARank engage when object-table
/// scan is ambiguous. Fed by conductor flags, HuntAlerts, and optional Sonar chat.
/// </summary>
public sealed class EngagePositionHint
{
	public Vector3? WorldPos { get; private set; }

	public uint TerritoryTypeId { get; private set; }

	public EngagePositionHintSource Source { get; private set; }

	public DateTimeOffset? UpdatedAt { get; private set; }

	public bool HasHint
		=> WorldPos is { } pos
			&& pos != Vector3.Zero
			&& Source != EngagePositionHintSource.None;

	/// <summary>Compact state diagnostic for helper logging.</summary>
	public string Describe()
		=> !HasHint || WorldPos is not { } pos
			? "hint=none"
			: $"hint={Source}, territory={TerritoryTypeId}, position=({pos.X:0.0},{pos.Y:0.0},{pos.Z:0.0})";

	/// <summary>XZ distance (yalms). Ignores Y so approximate map-link floors still rank.</summary>
	public static float DistanceXZ(Vector3 a, Vector3 b)
	{
		var dx = a.X - b.X;
		var dz = a.Z - b.Z;
		return MathF.Sqrt((dx * dx) + (dz * dz));
	}

	/// <summary>
	/// Store a world hint. Ignores zero/invalid positions.
	/// </summary>
	public void Remember(Vector3 worldPos, uint territoryTypeId, EngagePositionHintSource source)
	{
		if (source == EngagePositionHintSource.None)
			return;
		if (worldPos == Vector3.Zero
			|| float.IsNaN(worldPos.X)
			|| float.IsNaN(worldPos.Z)
			|| float.IsInfinity(worldPos.X)
			|| float.IsInfinity(worldPos.Z))
			return;

		WorldPos = worldPos;
		TerritoryTypeId = territoryTypeId;
		Source = source;
		UpdatedAt = DateTimeOffset.UtcNow;
	}

	/// <summary>
	/// Prefer resolved <see cref="HuntFlag.WorldPos"/>; else approximate XZ from raw map-link.
	/// </summary>
	public void RememberFromFlag(HuntFlag flag, EngagePositionHintSource source)
	{
		ArgumentNullException.ThrowIfNull(flag);
		if (flag.WorldPos is { } wp && wp != Vector3.Zero)
		{
			Remember(wp, flag.TerritoryTypeId, source);
			return;
		}

		Remember(
			FlagWorldPosition.ApproximateFromRaw(flag.RawX, flag.RawY),
			flag.TerritoryTypeId,
			source);
	}

	public void Clear()
	{
		WorldPos = null;
		TerritoryTypeId = 0;
		Source = EngagePositionHintSource.None;
		UpdatedAt = null;
	}

	/// <summary>
	/// Hint WorldPos when territory matches (or territory unknown / 0); otherwise null.
	/// </summary>
	public Vector3? WorldPosForTerritory(uint currentTerritory)
	{
		if (!HasHint || WorldPos is not { } pos)
			return null;
		if (TerritoryTypeId != 0
			&& currentTerritory != 0
			&& TerritoryTypeId != currentTerritory)
			return null;
		return pos;
	}
}
