#nullable enable

using System;
using HuntTrainAuto.Domain;

namespace HuntTrainAuto.HuntAlerts;

/// <summary>
/// HuntAlerts IPC Arrival is untrusted for aetheryte RowId (TASKS 10.3 follow-up / 10.5).
/// Chat path recomputes nearest via <c>TeleportDecision.Evaluate</c> /
/// <c>MapManager.GetNearestAetheryte</c>; strip IPC Arrival before that so auto-TP never
/// consumes IPC aetheryte ids as authoritative.
/// </summary>
public static class HuntAlertsArrivalTrust
{
	/// <summary>Compact, log-safe arrival-trust action summary.</summary>
	public static string Describe(int instance)
		=> instance > 0
			? $"cleared untrusted IPC arrival; retained instance={instance}"
			: "cleared untrusted IPC arrival; no instance hint";

	/// <summary>
	/// Clears <see cref="HuntFlag.Arrival"/> (IPC aetheryte). Returns a positive instance
	/// hint from Arrival or <see cref="HuntFlag.ReportedInstance"/> so evaluation can still
	/// target the hunt instance; otherwise 0.
	/// </summary>
	public static int ClearUntrustedArrival(HuntFlag flag, Action<string>? onDebug = null)
	{
		ArgumentNullException.ThrowIfNull(flag);

		var instance = flag.Arrival?.Instance ?? flag.ReportedInstance;
		flag.Arrival = null;
		if (instance > 0)
			flag.ReportedInstance = instance;
		var result = instance > 0 ? instance : 0;
		onDebug?.Invoke(Describe(result));
		return result;
	}
}
