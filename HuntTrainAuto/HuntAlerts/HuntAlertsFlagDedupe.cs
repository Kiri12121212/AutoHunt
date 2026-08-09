#nullable enable

using System;
using System.Numerics;
using HuntTrainAuto.Domain;
using HuntTrainAuto.Map;

namespace HuntTrainAuto.HuntAlerts;

/// <summary>
/// Intake source for cross-channel (chat ↔ HuntAlerts) duplicate suppression (TASKS 10.7).
/// </summary>
public enum HuntFlagIntakeSource
{
	Chat = 0,
	HuntAlerts = 1,
}

/// <summary>
/// Snapshot of the last flag accepted from chat or HuntAlerts (TASKS 10.7).
/// Owned by the plugin; helpers stay pure.
/// </summary>
public readonly struct HuntFlagDedupeMemory
{
	public HuntFlagDedupeMemory(
		HuntFlag flag,
		HuntFlagIntakeSource source,
		DateTimeOffset acceptedAt)
	{
		Flag = flag ?? throw new ArgumentNullException(nameof(flag));
		Source = source;
		AcceptedAt = acceptedAt;
	}

	public HuntFlag Flag { get; }
	public HuntFlagIntakeSource Source { get; }
	public DateTimeOffset AcceptedAt { get; }
}

/// <summary>
/// Same-flag / near-duplicate suppression for chat and HuntAlerts intake (TASKS 10.5)
/// plus windowed chat↔HuntAlerts cross-source dedupe (TASKS 10.7).
/// </summary>
public static class HuntAlertsFlagDedupe
{
	/// <summary>Reuse map-open duplicate distance (scaled map units from raw/1000).</summary>
	public const float NearDuplicateDistanceThreshold = MapOpenDedupe.DuplicateDistanceThreshold;

	/// <summary>
	/// Default window for suppressing the same hunt from the other intake source.
	/// Code constant (no Settings UI) — covers HA IPC and conductor chat arriving close together.
	/// </summary>
	public static readonly TimeSpan DefaultCrossSourceWindow = TimeSpan.FromSeconds(30);

	/// <summary>Compact, log-safe dedupe outcome summary.</summary>
	public static string Describe(bool suppressed, string scope)
		=> suppressed ? $"dedupe suppressed: {scope}" : $"dedupe accepted: {scope}";

	/// <summary>
	/// True when <paramref name="incoming"/> is the same territory and within
	/// <paramref name="distanceThreshold"/> of <paramref name="active"/> (scaled coords).
	/// Positive <see cref="ArrivalData.Instance"/> on incoming that differs from active
	/// is not a near-duplicate (same-spot re-flag for instance swap must proceed).
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

		var incomingInstance = ResolveReportedInstance(incoming);
		var activeInstance = ResolveReportedInstance(active);
		if (incomingInstance > 0 && incomingInstance != activeInstance)
			return false;

		var a = MapOpenDedupe.LinkPosFromRaw(active.RawX, active.RawY);
		var b = MapOpenDedupe.LinkPosFromRaw(incoming.RawX, incoming.RawY);
		return Vector2.Distance(a, b) <= distanceThreshold;
	}

	/// <summary>Arrival instance wins; else <see cref="HuntFlag.ReportedInstance"/>.</summary>
	public static int ResolveReportedInstance(HuntFlag flag)
	{
		ArgumentNullException.ThrowIfNull(flag);
		var arrival = flag.Arrival?.Instance ?? 0;
		if (arrival > 0)
			return arrival;
		return flag.ReportedInstance > 0 ? flag.ReportedInstance : 0;
	}

	/// <summary>
	/// Suppress chat or HuntAlerts intake when the flag is a near-duplicate of the active
	/// flag and the train pipeline is already running (avoid abort-restart churn mid-TP).
	/// Concurrent distinct flags still enter and abort-then-restart via FlagRestartDecision.
	/// Pass <paramref name="forceAccept"/> for deferred flush / world hand-off — that path
	/// must still strip Arrival trust and recompute nearest aetheryte (near-dup pipeline
	/// only; cross-source uses <see cref="ShouldSuppressCrossSource"/> without forceAccept).
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
	/// Cross-source (chat ↔ HuntAlerts) windowed suppress (TASKS 10.7).
	/// True when integration is on, memory is from the <em>other</em> source, the accept
	/// is still inside <paramref name="window"/>, and <paramref name="incoming"/> is a
	/// near-duplicate — first source wins; the other channel must not double-start.
	/// Same-source repeats are not suppressed here (near-dup pipeline uses
	/// <see cref="ShouldSuppress"/> on both chat and HuntAlerts).
	/// When integration is off or <paramref name="window"/> is non-positive → no-op (false).
	/// Deferred flush must still call this (no forceAccept bypass) so chat-won hunts
	/// cannot restart after a pending hand-off.
	/// </summary>
	public static bool ShouldSuppressCrossSource(
		HuntFlagDedupeMemory? memory,
		HuntFlag incoming,
		HuntFlagIntakeSource incomingSource,
		DateTimeOffset now,
		bool huntAlertsIntegration,
		TimeSpan? window = null,
		float distanceThreshold = NearDuplicateDistanceThreshold)
	{
		ArgumentNullException.ThrowIfNull(incoming);

		if (!huntAlertsIntegration)
			return false;

		var w = window ?? DefaultCrossSourceWindow;
		if (w <= TimeSpan.Zero)
			return false;

		if (memory is not { } mem)
			return false;

		if (mem.Source == incomingSource)
			return false;

		if (now - mem.AcceptedAt > w)
			return false;

		return IsNearDuplicate(mem.Flag, incoming, distanceThreshold);
	}

	/// <summary>
	/// When cross-source suppress drops an incoming HA flag, whether to clear the pending
	/// defer. True when there is no pending flag, or pending is a near-duplicate of the
	/// chat-won <paramref name="memoryFlag"/> (stale same-hunt defer). False when memory
	/// is absent or pending is a different hunt — do not clear merely because pending
	/// coords lie near the suppressed incoming (unrelated cross-world visit stays intact).
	/// </summary>
	public static bool ShouldClearPendingOnCrossSourceSuppress(
		HuntFlag? pendingFlag,
		HuntFlag? memoryFlag,
		float distanceThreshold = NearDuplicateDistanceThreshold)
	{
		if (pendingFlag == null)
			return true;

		if (memoryFlag == null)
			return false;

		return IsNearDuplicate(pendingFlag, memoryFlag, distanceThreshold);
	}

	/// <summary>
	/// Record a successful adopt for later cross-source checks (pure factory).
	/// </summary>
	public static HuntFlagDedupeMemory Remember(
		HuntFlag flag,
		HuntFlagIntakeSource source,
		DateTimeOffset acceptedAt)
		=> new(flag, source, acceptedAt);

	/// <summary>
	/// Clear intake memory (master-off / dispose). Pure helper for plugin + tests.
	/// </summary>
	public static HuntFlagDedupeMemory? Clear(HuntFlagDedupeMemory? _)
		=> null;

	/// <summary>
	/// Windowed suppress of a near-duplicate of the last accepted intake (any source).
	/// Covers same-source double-share (chat→chat / HA→HA) when the pipeline looks Idle
	/// — e.g. conductor re-flags the find spot while engage is still pathing after
	/// Combat→Idle with ReadyForGroundFollow. Cross-source still uses
	/// <see cref="ShouldSuppressCrossSource"/> (integration-gated); this fills the
	/// same-source gap. Non-positive <paramref name="window"/> → no-op.
	/// </summary>
	public static bool ShouldSuppressRecentNearDuplicate(
		HuntFlagDedupeMemory? memory,
		HuntFlag incoming,
		DateTimeOffset now,
		TimeSpan? window = null,
		float distanceThreshold = NearDuplicateDistanceThreshold)
	{
		ArgumentNullException.ThrowIfNull(incoming);

		var w = window ?? DefaultCrossSourceWindow;
		if (w <= TimeSpan.Zero)
			return false;

		if (memory is not { } mem)
			return false;

		if (now - mem.AcceptedAt > w)
			return false;

		return IsNearDuplicate(mem.Flag, incoming, distanceThreshold);
	}

	/// <summary>
	/// Conductor chat intake gate: near-dup while pipeline is active, recent same-spot
	/// re-share window, or cross-source window.
	/// True → skip adopt (no second Engaging / AbortThenRestart / engage path clear).
	/// Distinct flags and Idle near-dups outside the window still proceed.
	/// </summary>
	public static bool ShouldSuppressChatIntake(
		HuntFlag? activeFlag,
		HuntFlag incoming,
		bool pipelineActive,
		HuntFlagDedupeMemory? crossSourceMemory,
		DateTimeOffset now,
		bool huntAlertsIntegration,
		TimeSpan? crossSourceWindow = null,
		float distanceThreshold = NearDuplicateDistanceThreshold)
	{
		if (ShouldSuppress(
			    activeFlag,
			    incoming,
			    pipelineActive,
			    forceAccept: false,
			    distanceThreshold))
			return true;

		if (ShouldSuppressRecentNearDuplicate(
			    crossSourceMemory,
			    incoming,
			    now,
			    crossSourceWindow,
			    distanceThreshold))
			return true;

		return ShouldSuppressCrossSource(
			crossSourceMemory,
			incoming,
			HuntFlagIntakeSource.Chat,
			now,
			huntAlertsIntegration,
			crossSourceWindow,
			distanceThreshold);
	}

	/// <summary>
	/// <c>AbortVisitThenEnter</c> gate: when false, near-dup or cross-source would suppress
	/// Accept — skip <c>Lifestream.Abort</c> and pending clear so the in-flight visit stays intact.
	/// When true, Abort + clear pending + Accept may proceed.
	/// </summary>
	public static bool ShouldProceedAbortVisitThenEnter(
		HuntFlag? activeFlag,
		HuntFlag incoming,
		bool pipelineActive,
		HuntFlagDedupeMemory? crossSourceMemory = null,
		DateTimeOffset? now = null,
		bool huntAlertsIntegration = true,
		TimeSpan? crossSourceWindow = null,
		float distanceThreshold = NearDuplicateDistanceThreshold)
	{
		if (ShouldSuppress(
			    activeFlag,
			    incoming,
			    pipelineActive,
			    forceAccept: false,
			    distanceThreshold))
			return false;

		var at = now ?? DateTimeOffset.UtcNow;
		if (ShouldSuppressRecentNearDuplicate(
			    crossSourceMemory,
			    incoming,
			    at,
			    crossSourceWindow,
			    distanceThreshold))
			return false;

		if (ShouldSuppressCrossSource(
			    crossSourceMemory,
			    incoming,
			    HuntFlagIntakeSource.HuntAlerts,
			    at,
			    huntAlertsIntegration,
			    crossSourceWindow,
			    distanceThreshold))
			return false;

		return true;
	}
}
