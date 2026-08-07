#nullable enable
using System;

namespace HuntTrainAuto.Teleport;

/// <summary>Outcome of pure teleport decision (no IPC / cast).</summary>
public enum TeleportAction
{
	/// <summary>Do not teleport (disabled, close enough, missing data, etc.).</summary>
	Skip,

	/// <summary>Flag territory differs from player — teleport to nearest aetheryte.</summary>
	TeleportToZone,

	/// <summary>Same territory, target instance differs from current.</summary>
	SwitchInstance,

	/// <summary>Same territory/instance but player farther than distance threshold.</summary>
	TeleportBecauseFar,
}

/// <summary>Why <see cref="TeleportAction.Skip"/> was chosen.</summary>
public enum TeleportSkipReason
{
	None,
	PluginDisabled,
	AutoTeleportDisabled,
	AlreadyClose,
	MissingArrival,
	PlayerStateUnavailable,
}

/// <summary>Immutable result of <see cref="TeleportDecision.Decide"/>.</summary>
public readonly struct TeleportDecisionResult
{
	public required TeleportAction Action { get; init; }

	public TeleportSkipReason SkipReason { get; init; }

	/// <summary>Intended arrival when action is a teleport; null on skip.</summary>
	public ArrivalData? Arrival { get; init; }

	public bool ShouldTeleport =>
		Action is TeleportAction.TeleportToZone
			or TeleportAction.SwitchInstance
			or TeleportAction.TeleportBecauseFar;
}

/// <summary>
/// Pure teleport decision (HTA <c>ChatMessageHandler</c> zone/instance triggers +
/// same-zone distance skip via <c>AutoTeleportAetheryteDistanceDiff</c>).
/// Does not call Teleporter, Lifestream, or cast teleport.
/// </summary>
public static class TeleportDecision
{
	/// <summary>
	/// Decides whether to teleport for a conductor flag.
	/// </summary>
	/// <param name="enabled">Plugin enabled (<see cref="Configuration.Enabled"/>).</param>
	/// <param name="autoTeleport"><see cref="Configuration.AutoTeleport"/>.</param>
	/// <param name="currentTerritory">Player territory RowId.</param>
	/// <param name="flagTerritory">Flag / map-link territory RowId.</param>
	/// <param name="playerDistance">
	/// Distance from player to flag (map or world units — caller chooses consistently with
	/// <paramref name="distanceThreshold"/>). Null when player position is unavailable.
	/// </param>
	/// <param name="distanceThreshold">
	/// Same-zone skip threshold (<see cref="Configuration.AutoTeleportAetheryteDistanceDiff"/>).
	/// HTA default is 3f.
	/// </param>
	/// <param name="currentInstance">Player instance (0 = unknown / shared).</param>
	/// <param name="targetInstance">
	/// Desired instance (0 = unspecified → no instance switch). Non-zero compared to
	/// <paramref name="currentInstance"/>.
	/// </param>
	/// <param name="arrival">
	/// Pre-built arrival for the flag's nearest aetheryte. Required for teleport outcomes;
	/// missing arrival yields <see cref="TeleportSkipReason.MissingArrival"/>.
	/// </param>
	public static TeleportDecisionResult Decide(
		bool enabled,
		bool autoTeleport,
		uint currentTerritory,
		uint flagTerritory,
		float? playerDistance,
		float distanceThreshold,
		int currentInstance,
		int targetInstance,
		ArrivalData? arrival)
	{
		if (!enabled)
			return Skip(TeleportSkipReason.PluginDisabled);

		if (!autoTeleport)
			return Skip(TeleportSkipReason.AutoTeleportDisabled);

		if (flagTerritory != currentTerritory)
		{
			if (arrival == null)
				return Skip(TeleportSkipReason.MissingArrival);

			return Teleport(TeleportAction.TeleportToZone, arrival);
		}

		// Same territory — instance before distance (HTA checks instance switch next).
		if (NeedsInstanceSwitch(currentInstance, targetInstance))
		{
			if (arrival == null)
				return Skip(TeleportSkipReason.MissingArrival);

			return Teleport(TeleportAction.SwitchInstance, WithInstance(arrival, targetInstance));
		}

		if (playerDistance == null)
			return Skip(TeleportSkipReason.PlayerStateUnavailable);

		if (playerDistance.Value <= distanceThreshold)
			return Skip(TeleportSkipReason.AlreadyClose);

		if (arrival == null)
			return Skip(TeleportSkipReason.MissingArrival);

		return Teleport(TeleportAction.TeleportBecauseFar, arrival);
	}

	/// <summary>
	/// Instance switch when target is specified (non-zero) and differs from current.
	/// </summary>
	public static bool NeedsInstanceSwitch(int currentInstance, int targetInstance)
		=> targetInstance > 0 && currentInstance != targetInstance;

	/// <summary>
	/// HTA <c>AutoSwitchInstanceToOne</c>: zone-change arrivals use instance 1 when enabled, else 0.
	/// </summary>
	public static int ResolveZoneChangeInstance(bool autoSwitchInstanceToOne)
		=> autoSwitchInstanceToOne ? 1 : 0;

	/// <summary>
	/// Builds arrival on <paramref name="flag"/> from a snapshot and runs <see cref="Decide"/>.
	/// Soft-fails with <see cref="TeleportSkipReason.PlayerStateUnavailable"/> when snapshot is null.
	/// Does not teleport.
	/// </summary>
	public static TeleportDecisionResult Evaluate(
		bool enabled,
		bool autoTeleport,
		float distanceThreshold,
		bool autoSwitchInstanceToOne,
		HuntFlag flag,
		TeleportPlayerSnapshot? snapshot)
	{
		ArgumentNullException.ThrowIfNull(flag);

		if (snapshot == null)
			return Skip(TeleportSkipReason.PlayerStateUnavailable);

		var s = snapshot.Value;
		var targetInstance = s.TargetInstance;
		if (targetInstance == 0 && s.CurrentTerritory != flag.TerritoryTypeId)
			targetInstance = ResolveZoneChangeInstance(autoSwitchInstanceToOne);

		var arrival = ArrivalData.Attach(flag, s.Nearest, targetInstance);
		return Decide(
			enabled,
			autoTeleport,
			s.CurrentTerritory,
			flag.TerritoryTypeId,
			s.PlayerDistance,
			distanceThreshold,
			s.CurrentInstance,
			targetInstance,
			arrival);
	}

	private static TeleportDecisionResult Skip(TeleportSkipReason reason) => new()
	{
		Action = TeleportAction.Skip,
		SkipReason = reason,
		Arrival = null,
	};

	private static TeleportDecisionResult Teleport(TeleportAction action, ArrivalData arrival) => new()
	{
		Action = action,
		SkipReason = TeleportSkipReason.None,
		Arrival = arrival,
	};

	private static ArrivalData WithInstance(ArrivalData arrival, int instance)
	{
		if (arrival.Instance == instance)
			return arrival;

		return new ArrivalData
		{
			AetheryteId = arrival.AetheryteId,
			Territory = arrival.Territory,
			Instance = instance,
			World = arrival.World,
		};
	}
}
