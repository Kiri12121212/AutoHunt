#nullable enable

using System;
using System.Numerics;
using HuntTrainAuto.Domain;
using HuntTrainAuto.Map;

namespace HuntTrainAuto.HuntAlerts;

/// <summary>
/// Basic same-flag / near-duplicate suppression for HuntAlerts intake (TASKS 10.5).
/// Full chat↔HuntAlerts dedup window suite is 10.7 — this only suppresses restarting
/// the pipeline when the incoming flag is already the active near-duplicate.
/// </summary>
public static class HuntAlertsFlagDedupe
{
	/// <summary>Reuse map-open duplicate distance (scaled map units from raw/1000).</summary>
	public const float NearDuplicateDistanceThreshold = MapOpenDedupe.DuplicateDistanceThreshold;

	/// <summary>
	/// True when <paramref name="incoming"/> is the same territory and within
	/// <paramref name="distanceThreshold"/> of <paramref name="active"/> (scaled coords).
	/// </summary>
	public static bool IsNearDuplicate(
		HuntFlag? active,
		HuntFlag incoming,
		float distanceThreshold = NearDuplicateDistanceThreshold)
	{
		ArgumentNullException.ThrowIfNull(incoming);
		if (active == null)
			return false;

		if (active.TerritoryTypeId != incoming.TerritoryTypeId)
			return false;

		var a = MapOpenDedupe.LinkPosFromRaw(active.RawX, active.RawY);
		var b = MapOpenDedupe.LinkPosFromRaw(incoming.RawX, incoming.RawY);
		return Vector2.Distance(a, b) <= distanceThreshold;
	}

	/// <summary>
	/// Suppress HuntAlerts intake when the flag is a near-duplicate of the active flag
	/// and the train pipeline is already running (avoid abort-restart churn).
	/// Concurrent distinct flags still enter and abort-then-restart via FlagRestartDecision.
	/// Pass <paramref name="forceAccept"/> for deferred flush / world hand-off — that path
	/// must still strip Arrival trust and recompute nearest aetheryte.
	/// </summary>
	public static bool ShouldSuppress(
		HuntFlag? activeFlag,
		HuntFlag incoming,
		bool pipelineActive,
		bool forceAccept = false,
		float distanceThreshold = NearDuplicateDistanceThreshold)
		=> !forceAccept
		   && pipelineActive
		   && IsNearDuplicate(activeFlag, incoming, distanceThreshold);

	/// <summary>
	/// <c>AbortVisitThenEnter</c> gate: when false, near-dup would suppress Accept — skip
	/// <c>Lifestream.Abort</c> and pending clear so the in-flight visit stays intact.
	/// When true, Abort + clear pending + Accept may proceed.
	/// </summary>
	public static bool ShouldProceedAbortVisitThenEnter(
		HuntFlag? activeFlag,
		HuntFlag incoming,
		bool pipelineActive,
		float distanceThreshold = NearDuplicateDistanceThreshold)
		=> !ShouldSuppress(
			activeFlag,
			incoming,
			pipelineActive,
			forceAccept: false,
			distanceThreshold);
}
