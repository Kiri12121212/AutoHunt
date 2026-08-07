#nullable enable

namespace HuntTrainAuto.HuntAlerts;

/// <summary>
/// Whether a HuntAlerts flag should enter the train TP/nav pipeline after world-visit
/// (TASKS 10.5). Pure — no IPC / Dalamud.
/// </summary>
public enum HuntAlertsPipelineIntakeKind
{
	/// <summary>Do not start TP/nav (gated off, cannot visit, etc.).</summary>
	Skip,

	/// <summary>
	/// Feed <c>OnHuntFlagReceived</c>: same-world, or unknown current world with
	/// <em>no</em> pending defer (no <c>ChangeWorld</c> queued — safe to enter; may
	/// already be on hunt world). Unknown + occupied pending never enters here.
	/// </summary>
	EnterPipeline,

	/// <summary>
	/// Lifestream world visit was queued — stash the flag and enter the pipeline only
	/// after the player is on the hunt world (do not same-world TP while still visiting).
	/// Single pending slot: a newer defer replaces any prior (newest wins).
	/// </summary>
	DeferUntilOnWorld,
}

/// <summary>
/// How <see cref="HuntAlertsPipelineIntakeKind.EnterPipeline"/> interacts with an
/// existing cross-world pending defer (TASKS 10.5).
/// </summary>
public enum HuntAlertsEnterWithPendingKind
{
	/// <summary>No pending defer — clear is a no-op; enter the pipeline.</summary>
	Enter,

	/// <summary>
	/// Pending defer targets the same hunt world as the new flag — replace the pending
	/// flag (newest wins) and stay deferred until flush. Do not abort Lifestream or enter.
	/// </summary>
	ReplacePendingKeepDefer,

	/// <summary>
	/// Pending defer is for a different world than the new flag — abort the in-flight
	/// Lifestream visit, clear pending, then enter the pipeline for the new flag.
	/// Plugin must gate on near-dup suppress <em>before</em> Abort/clear (see
	/// <c>HuntAlertsFlagDedupe.ShouldProceedAbortVisitThenEnter</c>).
	/// </summary>
	AbortVisitThenEnter,
}

/// <summary>
/// How <see cref="HuntAlertsWorldVisitAction.BusyMidVisit"/> interacts with an
/// existing cross-world pending defer while Lifestream is still traveling (TASKS 10.5).
/// No <c>ChangeWorld</c> while busy — pending world must stay aligned with the in-flight visit.
/// </summary>
public enum HuntAlertsBusyDeferKind
{
	/// <summary>
	/// New flag targets the same world as the pending/in-flight visit — refresh the
	/// pending flag (newest wins); leave <c>pendingHuntAlertsWorld</c> unchanged.
	/// </summary>
	RefreshFlagKeepWorld,

	/// <summary>
	/// New flag targets a different world — skip; do not overwrite pending world
	/// (would desync flush from the Lifestream destination).
	/// </summary>
	SkipConflict,
}

/// <summary>
/// Maps <see cref="HuntAlertsWorldVisitAction"/> to pipeline intake (TASKS 10.5).
/// </summary>
public static class HuntAlertsPipelineIntake
{
	/// <summary>
	/// Decide skip / enter / defer from the world-visit outcome.
	/// <see cref="HuntAlertsWorldVisitAction.RequestWorldVisit"/> must not also start TP
	/// until the player is on the hunt world.
	/// <see cref="HuntAlertsWorldVisitAction.BusyMidVisit"/> with pending: same world →
	/// <see cref="HuntAlertsPipelineIntakeKind.DeferUntilOnWorld"/> (flag refresh only);
	/// different world → Skip (see <see cref="DecideBusyDeferRefresh"/>). With no pending
	/// it is Skip (true busy — distinct from gated-off <see cref="HuntAlertsWorldVisitAction.NoOp"/>).
	/// <see cref="HuntAlertsWorldVisitAction.UnknownCurrentWorld"/> with pending: same
	/// world → refresh defer (like BusyMidVisit); different world → Skip — never
	/// <see cref="HuntAlertsPipelineIntakeKind.EnterPipeline"/> /
	/// <see cref="HuntAlertsEnterWithPendingKind.AbortVisitThenEnter"/> while current is
	/// unreadable (loading/transitions; IsBusy may already be false). No pending → enter
	/// (visit skipped <c>ChangeWorld</c>; dropping the flag would lose the hunt).
	/// <see cref="HuntAlertsWorldVisitAction.DeferReplaceFailed"/> → defer Store for the
	/// incoming world (Abort already cleared the prior visit; never silent-drop the new flag).
	/// </summary>
	public static HuntAlertsPipelineIntakeKind Decide(
		HuntAlertsWorldVisitAction visitAction,
		bool hasPendingDefer = false,
		string? pendingWorld = null,
		string? newHuntWorld = null)
		=> visitAction switch
		{
			HuntAlertsWorldVisitAction.SameWorld => HuntAlertsPipelineIntakeKind.EnterPipeline,
			// Unknown current: enter only with empty pending. Occupied pending → same as
			// BusyMidVisit (refresh same-world / skip conflict) — do not AbortVisitThenEnter.
			HuntAlertsWorldVisitAction.UnknownCurrentWorld when !hasPendingDefer
				=> HuntAlertsPipelineIntakeKind.EnterPipeline,
			HuntAlertsWorldVisitAction.UnknownCurrentWorld when hasPendingDefer
				&& DecideBusyDeferRefresh(pendingWorld, newHuntWorld)
					== HuntAlertsBusyDeferKind.RefreshFlagKeepWorld
				=> HuntAlertsPipelineIntakeKind.DeferUntilOnWorld,
			HuntAlertsWorldVisitAction.RequestWorldVisit => HuntAlertsPipelineIntakeKind.DeferUntilOnWorld,
			// Abort + failed ChangeWorld: soft-clear prior via Store of incoming (newest retained).
			HuntAlertsWorldVisitAction.DeferReplaceFailed => HuntAlertsPipelineIntakeKind.DeferUntilOnWorld,
			// Busy mid-visit: same-world pending → refresh defer; different world → Skip.
			HuntAlertsWorldVisitAction.BusyMidVisit when hasPendingDefer
				&& DecideBusyDeferRefresh(pendingWorld, newHuntWorld)
					== HuntAlertsBusyDeferKind.RefreshFlagKeepWorld
				=> HuntAlertsPipelineIntakeKind.DeferUntilOnWorld,
			_ => HuntAlertsPipelineIntakeKind.Skip,
		};

	/// <summary>
	/// Resolve <see cref="HuntAlertsPipelineIntakeKind.EnterPipeline"/> when a cross-world
	/// defer may already have queued <c>ChangeWorld</c>.
	/// <list type="bullet">
	/// <item>
	/// No pending → enter (prior UnknownCurrentWorld / SameWorld behaviour).
	/// </item>
	/// <item>
	/// Pending world matches the new hunt world → keep/replace pending; do not clear or enter
	/// (avoids dropping the deferred hunt while the visit still completes).
	/// </item>
	/// <item>
	/// Pending world differs → abort Lifestream then clear + enter so the train cannot run
	/// on the current world while Lifestream still visits the prior hunt world.
	/// </item>
	/// </list>
	/// </summary>
	public static HuntAlertsEnterWithPendingKind DecideEnterWithPending(
		bool hasPendingDefer,
		string? pendingWorld,
		string? newHuntWorld)
	{
		if (!hasPendingDefer)
			return HuntAlertsEnterWithPendingKind.Enter;

		if (HuntAlertsWorldVisitDecision.IsSameWorld(pendingWorld, newHuntWorld))
			return HuntAlertsEnterWithPendingKind.ReplacePendingKeepDefer;

		return HuntAlertsEnterWithPendingKind.AbortVisitThenEnter;
	}

	/// <summary>
	/// Resolve <see cref="HuntAlertsWorldVisitAction.BusyMidVisit"/> against an existing
	/// pending defer. Same world → refresh flag only; different world → skip so
	/// <c>pendingHuntAlertsWorld</c> stays the Lifestream destination (no forever-wait flush).
	/// </summary>
	public static HuntAlertsBusyDeferKind DecideBusyDeferRefresh(
		string? pendingWorld,
		string? newHuntWorld)
	{
		if (HuntAlertsWorldVisitDecision.IsSameWorld(pendingWorld, newHuntWorld))
			return HuntAlertsBusyDeferKind.RefreshFlagKeepWorld;

		return HuntAlertsBusyDeferKind.SkipConflict;
	}

	/// <summary>
	/// True when a deferred cross-world flag may enter the pipeline
	/// (current world matches the pending hunt world).
	/// </summary>
	public static bool ShouldFlushDeferred(string? pendingWorld, string? currentWorldName)
		=> !string.IsNullOrEmpty(pendingWorld)
		   && HuntAlertsWorldVisitDecision.IsSameWorld(currentWorldName, pendingWorld);

	/// <summary>
	/// Soft-retry <c>ChangeWorld(pending)</c> on Framework tick when pending exists,
	/// Lifestream is not busy, current world is known, and the player is not yet on
	/// the pending world. Covers <see cref="HuntAlertsWorldVisitAction.DeferReplaceFailed"/>
	/// (Store without a queued visit) so the hunt does not stick forever waiting for
	/// manual travel / a later IPC. Flush remains the path when already on-world.
	/// Unknown current → false (may already be on pending; wait until readable).
	/// <paramref name="visitJustQueuedThisTick"/> → false: Drain/Process already
	/// attempted <c>ChangeWorld</c> this tick (success or fail) — do not re-issue
	/// (IsBusy may still be false; BusyMidVisit refresh after same-pending fail included).
	/// </summary>
	public static bool ShouldRetryPendingChangeWorld(
		bool hasPendingDefer,
		string? pendingWorld,
		string? currentWorldName,
		bool lifestreamBusy,
		bool visitJustQueuedThisTick = false)
	{
		if (visitJustQueuedThisTick)
			return false;
		if (!hasPendingDefer || string.IsNullOrEmpty(pendingWorld))
			return false;
		if (lifestreamBusy)
			return false;
		if (string.IsNullOrEmpty(currentWorldName))
			return false;
		// On pending world → flush, not ChangeWorld retry.
		if (ShouldFlushDeferred(pendingWorld, currentWorldName))
			return false;
		return true;
	}

	/// <summary>
	/// Single-slot defer policy: a second <see cref="HuntAlertsPipelineIntakeKind.DeferUntilOnWorld"/>
	/// replaces any prior pending flag/world (newest wins; prior is not flushed).
	/// BusyMidVisit same-world refresh replaces the flag only (pending world unchanged).
	/// UnknownCurrentWorld same-world with pending uses the same refresh path.
	/// </summary>
	public static bool DeferredStashReplacesPrior(bool hasPendingFlag) => hasPendingFlag;

	/// <summary>
	/// Whether a non-busy <see cref="HuntAlertsWorldVisitAction.RequestWorldVisit"/> that will
	/// replace an existing defer must soft-fail <c>Lifestream.Abort</c> before the new
	/// <c>ChangeWorld</c> (and before updating pending). Different target world → abort the
	/// prior visit so flush cannot wait forever on a world Lifestream is no longer visiting.
	/// Same-world replace → refresh only; no Abort.
	/// </summary>
	public static bool ShouldAbortPriorVisitOnDeferReplace(
		bool hasPendingDefer,
		string? pendingWorld,
		string? newHuntWorld)
	{
		if (!hasPendingDefer)
			return false;

		if (HuntAlertsWorldVisitDecision.IsSameWorld(pendingWorld, newHuntWorld))
			return false;

		return true;
	}
}
