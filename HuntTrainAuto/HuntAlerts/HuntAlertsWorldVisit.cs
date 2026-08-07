#nullable enable

using System;
using HuntTrainAuto.Contracts;
using HuntTrainAuto.Domain;

namespace HuntTrainAuto.HuntAlerts;

/// <summary>
/// Thin HuntAlerts → Lifestream world-visit wire (TASKS 10.4).
/// Does not run TP/mount/vnav (that is 10.5).
/// </summary>
public static class HuntAlertsWorldVisit
{
	/// <summary>
	/// Decide and optionally queue <c>Lifestream.ChangeWorld</c> for a mapped flag.
	/// Soft-fails: never throws; returns the decision taken.
	/// </summary>
	public static HuntAlertsWorldVisitDecisionResult TryHandle(
		HuntFlag flag,
		bool huntAlertsIntegration,
		ILifestreamService lifestream,
		string? currentWorldName)
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
		if (!queued)
			return new HuntAlertsWorldVisitDecisionResult
			{
				Action = HuntAlertsWorldVisitAction.NoOp,
				World = null,
			};

		return decision;
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
