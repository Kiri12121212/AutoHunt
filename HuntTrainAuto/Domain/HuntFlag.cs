#nullable enable
using System;
using System.Numerics;

namespace HuntTrainAuto.Domain;

/// <summary>
/// Conductor map-link flag extracted from chat payloads.
/// <see cref="WorldPos"/> is set via <see cref="FlagWorldPosition.Attach"/> /
/// <see cref="FlagWorldHelper"/> (PointOnFloor); <see cref="Arrival"/> is filled in phase 3.
/// </summary>
public sealed class HuntFlag
{
	public required uint TerritoryTypeId { get; init; }

	public required uint MapId { get; init; }

	public required int RawX { get; init; }

	public required int RawY { get; init; }

	public string? PlaceName { get; init; }

	public DateTimeOffset Timestamp { get; init; }

	/// <summary>
	/// Navmesh floor position for <see cref="MovementHelper.Move"/>; null until PointOnFloor succeeds.
	/// </summary>
	public Vector3? WorldPos { get; set; }

	/// <summary>Teleport plan from nearest-aetheryte selection (phase 3).</summary>
	public ArrivalData? Arrival { get; set; }

	/// <summary>
	/// Pure factory from map-link fields (unit-testable without Dalamud payloads).
	/// </summary>
	public static HuntFlag FromMapLink(
		uint territoryTypeId,
		uint mapId,
		int rawX,
		int rawY,
		string? placeName,
		DateTimeOffset? timestamp = null)
	{
		var trimmed = placeName?.Trim();
		return new HuntFlag
		{
			TerritoryTypeId = territoryTypeId,
			MapId = mapId,
			RawX = rawX,
			RawY = rawY,
			PlaceName = string.IsNullOrEmpty(trimmed) ? null : trimmed,
			Timestamp = timestamp ?? DateTimeOffset.UtcNow,
		};
	}
}
