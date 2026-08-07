#nullable enable
using System;

namespace HuntTrainAuto;

/// <summary>
/// Teleport plan for a hunt flag: aetheryte RowId, territory, instance, optional world.
/// Pure ids only — no Lumina sheet rows (sheet checks stay in game-bound wrappers).
/// </summary>
public sealed class ArrivalData
{
	public required uint AetheryteId { get; init; }

	public required uint Territory { get; init; }

	/// <summary>Instance number (0 = unspecified / shared world).</summary>
	public required int Instance { get; init; }

	public string? World { get; init; }

	/// <summary>
	/// Creates an arrival plan, or null when <paramref name="aetheryteId"/> is invalid (0).
	/// Does not consult Excel sheets.
	/// </summary>
	public static ArrivalData? CreateOrNull(
		uint aetheryteId,
		uint territory,
		int instance,
		string? world = null)
	{
		if (aetheryteId == 0)
			return null;

		var trimmed = world?.Trim();
		return new ArrivalData
		{
			AetheryteId = aetheryteId,
			Territory = territory,
			Instance = instance,
			World = string.IsNullOrEmpty(trimmed) ? null : trimmed,
		};
	}

	/// <summary>
	/// Bridge from nearest-aetheryte selection; null when no aetheryte was selected
	/// or its RowId is invalid.
	/// </summary>
	public static ArrivalData? CreateOrNull(
		NearestAetheryteResult? nearest,
		uint territory,
		int instance,
		string? world = null)
	{
		if (nearest == null)
			return null;

		return CreateOrNull(nearest.Value.RowId, territory, instance, world);
	}

	/// <summary>
	/// Builds arrival from nearest result using <see cref="HuntFlag.TerritoryTypeId"/>
	/// and assigns <see cref="HuntFlag.Arrival"/> (clears when create returns null).
	/// Does not teleport.
	/// </summary>
	public static ArrivalData? Attach(
		HuntFlag flag,
		NearestAetheryteResult? nearest,
		int instance,
		string? world = null)
	{
		ArgumentNullException.ThrowIfNull(flag);
		var arrival = CreateOrNull(nearest, flag.TerritoryTypeId, instance, world);
		flag.Arrival = arrival;
		return arrival;
	}
}
