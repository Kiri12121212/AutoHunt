#nullable enable

using System;
using HuntTrainAuto.Contracts;
using HuntTrainAuto.Domain;

namespace HuntTrainAuto.HuntAlerts;

/// <summary>
/// Thin HuntAlerts → Lifestream world-visit wire (TASKS 10.4).
/// Pipeline intake after SameWorld / deferred flush is TASKS 10.5.
/// </summary>
public static class HuntAlertsWorldVisit
{
	/// <summary>
	/// Decide and optionally queue <c>Lifestream.ChangeWorld</c> for a mapped flag.
	/// Soft-fails: never throws; returns the decision taken.
	/// When replacing a pending defer for a <em>different</em> world, soft-fails
	/// <c>Abort</c> before <c>ChangeWorld</c> so the prior visit cannot complete
	/// against a pending slot that already names the new world (TASKS 10.5).
	/// If <c>ChangeWorld</c> still fails after Abort, retries once when Lifestream
	/// is no longer busy; otherwise returns
	/// <see cref="HuntAlertsWorldVisitAction.DeferReplaceFailed"/> with the incoming
	/// world so Plugin can Store it (never silent-drop).
	/// </summary>
	/// <param name="hasPendingDefer">True when a cross-world defer slot is occupied.</param>
	/// <param name="pendingDeferWorld">World named by the pending defer (if any).</param>
	public static HuntAlertsWorldVisitDecisionResult TryHandle(
		HuntFlag flag,
		bool huntAlertsIntegration,
		ILifestreamService lifestream,
		string? currentWorldName,
		bool hasPendingDefer = false,
		string? pendingDeferWorld = null)
	{
		ArgumentNullException.ThrowIfNull(flag);
		ArgumentNullException.ThrowIfNull(lifestream);

		// Prefer HuntWorld (set even when Arrival is omitted for aetheryte id 0).
		var huntWorld = FirstNonBlank(flag.HuntWorld, flag.Arrival?.World);
		var sanitizedOk = HuntAlertsWorldVisitDecision.TrySanitizeWorldName(
			huntWorld,
			out var sanitized);

		var available = false;
		var busy = false;
		var canSame = false;
		var canCross = false;

		try
		{
			available = lifestream.IsAvailable;
		}
		catch
		{
			available = false;
		}

		if (available)
		{
			try
			{
				busy = lifestream.IsBusy();
			}
			catch
			{
				busy = false;
			}

			if (sanitizedOk)
			{
				try
				{
					canSame = lifestream.CanVisitSameDC(sanitized);
				}
				catch
				{
					canSame = false;
				}

				try
				{
					canCross = lifestream.CanVisitCrossDC(sanitized);
				}
				catch
				{
					canCross = false;
				}
			}
		}

		var decision = HuntAlertsWorldVisitDecision.Decide(
			huntAlertsIntegration,
			available,
			busy,
			currentWorldName,
			huntWorld,
			canSame,
			canCross);

		if (decision.Action != HuntAlertsWorldVisitAction.RequestWorldVisit
		    || string.IsNullOrEmpty(decision.World))
			return decision;

		// Different-world defer replace: Abort prior visit before queuing the new one.
		// Store pending only after successful ChangeWorld (Plugin Defers on RequestWorldVisit).
		var abortedPrior = HuntAlertsPipelineIntake.ShouldAbortPriorVisitOnDeferReplace(
			hasPendingDefer,
			pendingDeferWorld,
			decision.World);
		if (abortedPrior)
		{
			try
			{
				lifestream.Abort();
			}
			catch
			{
				// Soft-fail: mirror ChangeWorld / Plugin Abort paths.
			}
		}

		bool queued;
		try
		{
			queued = lifestream.ChangeWorld(decision.World);
		}
		catch
		{
			// Soft-fail: ChangeWorld wrapper should already catch; belt-and-suspenders.
			queued = false;
		}

		// Only claim RequestWorldVisit when Lifestream accepted the queue.
		// AttemptedChangeWorld is true on every post-ChangeWorld return so Plugin can
		// skip same-tick pending soft-retry (BusyMidVisit refresh / DeferReplaceFailed /
		// NoOp fail — not only RequestWorldVisit success).
		if (!queued)
		{
			if (abortedPrior)
			{
				// Abort killed the prior visit. If Lifestream is no longer busy, retry
				// ChangeWorld once for the incoming world before giving up.
				queued = TryChangeWorldOnceMoreIfNotBusy(lifestream, decision.World);
				if (queued)
					return decision with { AttemptedChangeWorld = true };

				// Still failed — retain incoming as pending (World set for Store).
				// Never silent-drop the new hunt after Abort wiped the prior visit.
				return new HuntAlertsWorldVisitDecisionResult
				{
					Action = HuntAlertsWorldVisitAction.DeferReplaceFailed,
					World = decision.World,
					AttemptedChangeWorld = true,
				};
			}

			// Same-world pending replace: ChangeWorld failed without Abort — return
			// BusyMidVisit with World so intake RefreshFlagKeepWorld updates the flag
			// (NoOp+null World → Skip would keep the older pending flag).
			if (hasPendingDefer
			    && HuntAlertsWorldVisitDecision.IsSameWorld(pendingDeferWorld, decision.World))
			{
				return new HuntAlertsWorldVisitDecisionResult
				{
					Action = HuntAlertsWorldVisitAction.BusyMidVisit,
					World = decision.World,
					AttemptedChangeWorld = true,
				};
			}

			return new HuntAlertsWorldVisitDecisionResult
			{
				Action = HuntAlertsWorldVisitAction.NoOp,
				World = null,
				AttemptedChangeWorld = true,
			};
		}

		return decision with { AttemptedChangeWorld = true };
	}

	/// <summary>
	/// After Abort + failed <c>ChangeWorld</c>: if Lifestream reports not busy, attempt
	/// one more <c>ChangeWorld</c>. Soft-fails; never throws.
	/// </summary>
	private static bool TryChangeWorldOnceMoreIfNotBusy(ILifestreamService lifestream, string world)
	{
		bool busy;
		try
		{
			busy = lifestream.IsBusy();
		}
		catch
		{
			busy = false;
		}

		if (busy)
			return false;

		try
		{
			return lifestream.ChangeWorld(world);
		}
		catch
		{
			return false;
		}
	}

	private static string? FirstNonBlank(string? primary, string? fallback)
	{
		var a = primary?.Trim();
		if (!string.IsNullOrEmpty(a))
			return a;
		var b = fallback?.Trim();
		return string.IsNullOrEmpty(b) ? null : b;
	}
}
