#nullable enable
using System;
using System.Numerics;

namespace HuntTrainAuto;

/// <summary>
/// Conductor map-link flag extracted from chat payloads.
/// <see cref="WorldPos"/> / <see cref="Arrival"/> are phase 3–4 stubs.
/// </summary>
public sealed class HuntFlag
{
	public required uint TerritoryTypeId { get; init; }

	public required uint MapId { get; init; }

	public required int RawX { get; init; }

	public required int RawY { get; init; }

	public string? PlaceName { get; init; }

	public DateTimeOffset Timestamp { get; init; }

	/// <summary>Filled later via mesh / floor query (phase 4).</summary>
	public Vector3? WorldPos { get; set; }

	/// <summary>Filled later by nearest-aetheryte selection (phase 3).</summary>
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

/// <summary>Phase 3 stub for nearest aetheryte / instance plan.</summary>
public sealed class ArrivalData
{
}
