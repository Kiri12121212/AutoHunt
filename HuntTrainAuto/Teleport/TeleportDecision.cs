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
	/// <summary>Same-zone time-aware pathfind still running — defer far-TP adopt.</summary>
	AwaitingTravelCost,
}

/// <summary>Immutable result of <see cref="TeleportDecision.Decide"/>.</summary>
public readonly struct TeleportDecisionResult
{
	public required TeleportAction Action { get; init; }

	public TeleportSkipReason SkipReason { get; init; }

	/// <summary>Intended arrival when action is a teleport; null on skip.</summary>
	public ArrivalData? Arrival { get; init; }


	public string Describe()
	{
		var arrival = Arrival == null
			? "arrival=none"
			: $"arrival=aetheryte:{Arrival.AetheryteId}, territory:{Arrival.Territory}, instance:{Arrival.Instance}";
		return Action == TeleportAction.Skip
			? $"action={Action}, skip={SkipReason}, {arrival}"
			: $"action={Action}, {arrival}";
	}

	public bool ShouldTeleport =>
		Action is TeleportAction.TeleportToZone
			or TeleportAction.SwitchInstance
			or TeleportAction.TeleportBecauseFar;
}

/// <summary>
/// Pure teleport decision (HTA <c>ChatMessageHandler</c> zone/instance triggers +
/// same-zone distance skip via <c>AutoTeleportAetheryteDistanceDiff</c>,
/// optional time-aware path-cost compare).
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
	/// World XZ distance from player to flag in yalms. Null when player position is unavailable.
	/// </param>
	/// <param name="distanceThreshold">
	/// Same-zone skip threshold in yalms
	/// (<see cref="Configuration.AutoTeleportAetheryteDistanceDiff"/>).
	/// Default <c>150f</c> (~former 3 map-units).
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
	/// <param name="timeAware">
	/// Optional same-zone time-aware settings. When enabled with Ready path lengths,
	/// compares direct vs TP travel time; Pending defers; Unavailable soft-falls to distance
	/// / player-closer-than-aetheryte.
	/// </param>
	/// <param name="travelEstimate">Injected path lengths / status (null = distance only).</param>
	/// <param name="aetheryteDistance">
	/// World XZ aetheryte→flag yalms. Used when path-cost is Unavailable so we skip TP when
	/// the player is already closer than the aetheryte.
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
		ArrivalData? arrival,
		SameZoneTimeAwareSettings timeAware = default,
		SameZoneTravelEstimate? travelEstimate = null,
		float? aetheryteDistance = null)
	{
		if (!enabled)
			return Skip(TeleportSkipReason.PluginDisabled);

		var sameZone = flagTerritory == currentTerritory;
		var needsInstanceSwitch = sameZone
			&& NeedsInstanceSwitch(currentInstance, targetInstance);

		// Instance swap is independent of AutoTeleport (Lifestream ChangeInstance, not aetheryte TP).
		if (!autoTeleport && !needsInstanceSwitch)
			return Skip(TeleportSkipReason.AutoTeleportDisabled);

		if (!sameZone)
		{
			if (!autoTeleport)
				return Skip(TeleportSkipReason.AutoTeleportDisabled);

			if (arrival == null)
				return Skip(TeleportSkipReason.MissingArrival);

			return Teleport(TeleportAction.TeleportToZone, arrival);
		}

		// Same territory — conductor/flag instance wins over distance skip (ChangeInstance, no aetheryte TP).
		if (needsInstanceSwitch)
		{
			if (arrival == null)
				return Skip(TeleportSkipReason.MissingArrival);

			return Teleport(TeleportAction.SwitchInstance, WithInstance(arrival, targetInstance));
		}

		if (!autoTeleport)
			return Skip(TeleportSkipReason.AutoTeleportDisabled);

		if (playerDistance is { } d0)
		{
			var withinDistanceFloor = d0 <= distanceThreshold;
			if (withinDistanceFloor && (!timeAware.Enabled || timeAware.RetainDistanceAsFloor))
				return Skip(TeleportSkipReason.AlreadyClose);
		}

		if (playerDistance == null)
			return Skip(TeleportSkipReason.PlayerStateUnavailable);

		if (timeAware.Enabled)
		{
			var estimate = travelEstimate ?? new SameZoneTravelEstimate
			{
				Status = SameZonePathCostStatus.Unavailable,
			};

			if (estimate.Status == SameZonePathCostStatus.Pending)
				return Skip(TeleportSkipReason.AwaitingTravelCost);

			var skipForTime = SameZoneTravelCost.ShouldSkipTeleportForTime(estimate, timeAware);
			if (skipForTime == true)
				return Skip(TeleportSkipReason.AlreadyClose);
			if (skipForTime == false)
			{
				if (arrival == null)
					return Skip(TeleportSkipReason.MissingArrival);
				return Teleport(TeleportAction.TeleportBecauseFar, arrival);
			}

			// Unavailable / invalid estimate — soft-fall to yalm floor or closer-than-aetheryte.
			if (SameZoneTravelCost.ShouldSkipTeleportWhenPathCostUnavailable(
				    playerDistance,
				    aetheryteDistance,
				    distanceThreshold))
				return Skip(TeleportSkipReason.AlreadyClose);
		}

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
	/// Prefer conductor/flag-reported instance; else zone-change force-to-1 when enabled.
	/// </summary>
	public static int ResolveTargetInstance(
		int reportedInstance,
		bool zoneChange,
		bool autoSwitchInstanceToOne)
	{
		if (reportedInstance > 0)
			return reportedInstance;

		if (zoneChange)
			return ResolveZoneChangeInstance(autoSwitchInstanceToOne);

		return 0;
	}

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
		TeleportPlayerSnapshot? snapshot,
		SameZoneTimeAwareSettings timeAware = default)
	{
		ArgumentNullException.ThrowIfNull(flag);

		if (snapshot == null)
			return Skip(TeleportSkipReason.PlayerStateUnavailable);

		var s = snapshot.Value;
		var zoneChange = s.CurrentTerritory != flag.TerritoryTypeId;
		// Snapshot hint first; else flag.ReportedInstance (chat/HA may only set the flag).
		var reported = s.TargetInstance > 0 ? s.TargetInstance : flag.ReportedInstance;
		var targetInstance = ResolveTargetInstance(
			reported,
			zoneChange,
			autoSwitchInstanceToOne);

		ArrivalData? arrival;
		if (!zoneChange && NeedsInstanceSwitch(s.CurrentInstance, targetInstance))
		{
			// ChangeInstance does not need an aetheryte row; keep nearest when known for Approach.
			arrival = ArrivalData.AttachForInstanceSwitch(flag, s.Nearest, targetInstance);
		}
		else
		{
			arrival = ArrivalData.Attach(flag, s.Nearest, targetInstance);
		}

		return Decide(
			enabled,
			autoTeleport,
			s.CurrentTerritory,
			flag.TerritoryTypeId,
			s.PlayerDistance,
			distanceThreshold,
			s.CurrentInstance,
			targetInstance,
			arrival,
			timeAware,
			s.TravelEstimate,
			s.AetheryteDistance);
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
