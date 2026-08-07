#nullable enable

using System;

namespace HuntTrainAuto.HuntAlerts;

/// <summary>
/// Outcome of a HuntAlerts cross-world visit decision (TASKS 10.4).
/// </summary>
public enum HuntAlertsWorldVisitAction
{
	/// <summary>Do nothing (gated off, missing data, or unknown world).</summary>
	NoOp,

	/// <summary>Hunt is on the player's current world — leave for same-world TP/nav (10.5).</summary>
	SameWorld,

	/// <summary>
	/// Player current world is null/unknown and Lifestream is not busy — do not
	/// queue <c>ChangeWorld</c> (may already be on the hunt world). Intake enters
	/// only when pending is empty; with pending, same/diff refresh/skip like
	/// <see cref="BusyMidVisit"/> (never Abort while current is unreadable).
	/// When busy, prefer <see cref="BusyMidVisit"/> so intake can use pending.
	/// </summary>
	UnknownCurrentWorld,

	/// <summary>Queue Lifestream <c>ChangeWorld</c> for <see cref="HuntAlertsWorldVisitDecisionResult.World"/>.</summary>
	RequestWorldVisit,

	/// <summary>
	/// Lifestream is busy mid-visit — do not re-issue <c>ChangeWorld</c>.
	/// Distinct from <see cref="NoOp"/> so intake can refresh a same-world pending
	/// flag (newest wins) without moving <c>pendingHuntAlertsWorld</c>; different-world
	/// updates while busy are skipped (see <see cref="HuntAlertsPipelineIntake.DecideBusyDeferRefresh"/>).
	/// Also returned when same-world pending replace fails <c>ChangeWorld</c> without Abort
	/// so intake still refreshes the pending flag (NoOp would Skip and keep the older flag).
	/// </summary>
	BusyMidVisit,

	/// <summary>Different world but Lifestream cannot visit it (not in same/cross-DC lists).</summary>
	CannotVisit,

	/// <summary>
	/// Different-world defer replace called <c>Abort</c> then <c>ChangeWorld</c> failed
	/// (after one not-busy retry). <see cref="World"/> is the incoming hunt world —
	/// Plugin soft-clears the aborted prior pending and Stores the incoming flag
	/// (newest retained; never silent drop after Abort).
	/// </summary>
	DeferReplaceFailed,
}

/// <summary>Pure decision result; no IPC side effects.</summary>
public readonly struct HuntAlertsWorldVisitDecisionResult
{
	public required HuntAlertsWorldVisitAction Action { get; init; }

	/// <summary>
	/// Sanitized target world when <see cref="Action"/> is
	/// <see cref="HuntAlertsWorldVisitAction.RequestWorldVisit"/>,
	/// <see cref="HuntAlertsWorldVisitAction.BusyMidVisit"/>,
	/// <see cref="HuntAlertsWorldVisitAction.DeferReplaceFailed"/>,
	/// or other world-bearing outcomes.
	/// </summary>
	public string? World { get; init; }

	/// <summary>
	/// True when <see cref="HuntAlertsWorldVisit.TryHandle"/> invoked
	/// <c>Lifestream.ChangeWorld</c> this call (success or fail), including the
	/// Abort+retry path and same-pending remap to <see cref="HuntAlertsWorldVisitAction.BusyMidVisit"/>.
	/// Plugin uses this to skip same-tick pending soft-retry — not only on
	/// <see cref="HuntAlertsWorldVisitAction.RequestWorldVisit"/> success.
	/// </summary>
	public bool AttemptedChangeWorld { get; init; }
}

/// <summary>
/// Pure HTA <c>SonarMonitor.HandleAutoTeleport</c> world-branch gates for HuntAlerts.
/// Callers supply Lifestream availability / <c>CanVisit*</c> probes; no CallGate here.
/// </summary>
public static class HuntAlertsWorldVisitDecision
{
	/// <summary>
	/// Decide whether to request a Lifestream world visit, proceed same-world, or no-op.
	/// </summary>
	/// <param name="huntAlertsIntegration"><see cref="Configuration.HuntAlertsIntegration"/>.</param>
	/// <param name="lifestreamAvailable"><c>ILifestreamService.IsAvailable</c>.</param>
	/// <param name="lifestreamBusy"><c>ILifestreamService.IsBusy</c> — mid-visit; see <see cref="HuntAlertsWorldVisitAction.BusyMidVisit"/>.</param>
	/// <param name="currentWorldName">Player current world display name (trimmed comparison).</param>
	/// <param name="arrivalWorld">Hunt world from <see cref="Domain.HuntFlag.HuntWorld"/> / Arrival.</param>
	/// <param name="canVisitSameDc"><c>Lifestream.CanVisitSameDC</c> for the sanitized arrival world.</param>
	/// <param name="canVisitCrossDc"><c>Lifestream.CanVisitCrossDC</c> for the sanitized arrival world.</param>
	public static HuntAlertsWorldVisitDecisionResult Decide(
		bool huntAlertsIntegration,
		bool lifestreamAvailable,
		bool lifestreamBusy,
		string? currentWorldName,
		string? arrivalWorld,
		bool canVisitSameDc,
		bool canVisitCrossDc)
	{
		if (!huntAlertsIntegration)
			return NoOp();

		if (!TrySanitizeWorldName(arrivalWorld, out var target))
			return NoOp();

		// Unknown current world: do not treat as cross-world (risk of visiting while already there).
		// Exception: Lifestream busy mid-visit → BusyMidVisit even when current is unknown so
		// intake can refresh/skip against pending. Not-busy UnknownCurrentWorld with pending
		// is also refresh/skip in intake (not EnterPipeline / AbortVisitThenEnter).
		var current = currentWorldName?.Trim();
		if (string.IsNullOrEmpty(current))
		{
			if (lifestreamAvailable && lifestreamBusy)
			{
				return new HuntAlertsWorldVisitDecisionResult
				{
					Action = HuntAlertsWorldVisitAction.BusyMidVisit,
					World = target,
				};
			}

			return new HuntAlertsWorldVisitDecisionResult
			{
				Action = HuntAlertsWorldVisitAction.UnknownCurrentWorld,
				World = target,
			};
		}

		if (IsSameWorld(current, target))
			return new HuntAlertsWorldVisitDecisionResult
			{
				Action = HuntAlertsWorldVisitAction.SameWorld,
				World = target,
			};

		// Cross-world requires Lifestream; never blind-Execute arbitrary strings.
		if (!lifestreamAvailable)
			return NoOp();

		if (lifestreamBusy)
		{
			// Do not queue ChangeWorld while busy; keep World so intake can compare
			// against pending and refresh same-world defer only.
			return new HuntAlertsWorldVisitDecisionResult
			{
				Action = HuntAlertsWorldVisitAction.BusyMidVisit,
				World = target,
			};
		}

		// Whitelist: only worlds Lifestream recognizes as visitable.
		if (!canVisitSameDc && !canVisitCrossDc)
		{
			return new HuntAlertsWorldVisitDecisionResult
			{
				Action = HuntAlertsWorldVisitAction.CannotVisit,
				World = target,
			};
		}

		return new HuntAlertsWorldVisitDecisionResult
		{
			Action = HuntAlertsWorldVisitAction.RequestWorldVisit,
			World = target,
		};
	}

	/// <summary>
	/// Trim + non-empty gate. Rejects strings that cannot be FFXIV world names
	/// (control chars / path-like junk) without maintaining a full world sheet.
	/// Known-world whitelist is <paramref name="canVisitSameDc"/> / cross-DC at decide time.
	/// </summary>
	public static bool TrySanitizeWorldName(string? world, out string sanitized)
	{
		sanitized = null!;
		if (string.IsNullOrWhiteSpace(world))
			return false;

		var trimmed = world.Trim();
		if (trimmed.Length == 0 || trimmed.Length > 32)
			return false;

		for (var i = 0; i < trimmed.Length; i++)
		{
			var c = trimmed[i];
			if (char.IsAsciiLetter(c))
				continue;
			if (c is '\'' or '-' or ' ')
				continue;
			return false;
		}

		sanitized = trimmed;
		return true;
	}

	/// <summary>Case-insensitive name equality after trim (HTA <c>Player.CurrentWorld == world</c>).</summary>
	public static bool IsSameWorld(string? currentWorldName, string? arrivalWorld)
	{
		var current = currentWorldName?.Trim();
		var arrival = arrivalWorld?.Trim();
		if (string.IsNullOrEmpty(current) || string.IsNullOrEmpty(arrival))
			return false;

		return string.Equals(current, arrival, StringComparison.OrdinalIgnoreCase);
	}

	private static HuntAlertsWorldVisitDecisionResult NoOp()
		=> new() { Action = HuntAlertsWorldVisitAction.NoOp, World = null };
}
