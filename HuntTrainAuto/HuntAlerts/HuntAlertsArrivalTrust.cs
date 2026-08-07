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
	/// <summary>
	/// Clears <see cref="HuntFlag.Arrival"/> (IPC aetheryte). Returns a positive instance
	/// hint from the discarded Arrival when present so chat-path evaluation can still
	/// target the hunt instance; otherwise 0.
	/// </summary>
	public static int ClearUntrustedArrival(HuntFlag flag)
	{
		ArgumentNullException.ThrowIfNull(flag);

		var instance = flag.Arrival?.Instance ?? 0;
		flag.Arrival = null;
		return instance > 0 ? instance : 0;
	}
}
