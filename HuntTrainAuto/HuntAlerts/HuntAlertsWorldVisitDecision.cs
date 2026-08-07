#nullable enable

using System;

namespace HuntTrainAuto.HuntAlerts;

/// <summary>
/// Outcome of a HuntAlerts cross-world visit decision (TASKS 10.4).
/// </summary>
public enum HuntAlertsWorldVisitAction
{
	/// <summary>Do nothing (gated off, busy, missing data, or unknown world).</summary>
	NoOp,

	/// <summary>Hunt is on the player's current world — leave for same-world TP/nav (10.5).</summary>
	SameWorld,

	/// <summary>
	/// Player current world is null/unknown — do not queue <c>ChangeWorld</c>
	/// (may already be on the hunt world).
	/// </summary>
	UnknownCurrentWorld,

	/// <summary>Queue Lifestream <c>ChangeWorld</c> for <see cref="HuntAlertsWorldVisitDecisionResult.World"/>.</summary>
	RequestWorldVisit,

	/// <summary>Different world but Lifestream cannot visit it (not in same/cross-DC lists).</summary>
	CannotVisit,
}

/// <summary>Pure decision result; no IPC side effects.</summary>
public readonly struct HuntAlertsWorldVisitDecisionResult
{
	public required HuntAlertsWorldVisitAction Action { get; init; }

	/// <summary>Sanitized target world when <see cref="Action"/> is <see cref="HuntAlertsWorldVisitAction.RequestWorldVisit"/>.</summary>
	public string? World { get; init; }
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
	/// <param name="lifestreamBusy"><c>ILifestreamService.IsBusy</c> — HTA aborts when busy.</param>
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
		var current = currentWorldName?.Trim();
		if (string.IsNullOrEmpty(current))
		{
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
			return NoOp();

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
