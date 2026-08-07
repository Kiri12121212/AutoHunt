#nullable enable

using System;
using System.Threading;
using HuntTrainAuto.Domain;

namespace HuntTrainAuto.HuntAlerts;

/// <summary>
/// One deferred cross-world HuntAlerts flag + Lifestream destination (TASKS 10.5).
/// </summary>
public sealed class HuntAlertsPendingDefer
{
	public HuntAlertsPendingDefer(HuntFlag flag, string world)
	{
		Flag = flag;
		World = world;
	}

	public HuntFlag Flag { get; }

	/// <summary>Sanitized hunt world Lifestream is (or was) visiting.</summary>
	public string World { get; }
}

/// <summary>
/// Single-slot pending defer accessors (TASKS 10.5).
/// HuntAlerts IPC enqueues flags; <c>Framework.Update</c> drains then mutates this slot
/// (Abort / ChangeWorld / Store / Take on one tick thread). Interlocked swaps remain
/// belt-and-suspenders for re-entrancy / static analysis so a newer stash between
/// snapshot and clear is not dropped.
/// </summary>
public static class HuntAlertsPendingDeferSlot
{
	/// <summary>Replace the entire slot (newest wins).</summary>
	public static void Store(ref HuntAlertsPendingDefer? slot, HuntFlag flag, string world)
		=> Volatile.Write(ref slot, new HuntAlertsPendingDefer(flag, world));

	/// <summary>
	/// BusyMidVisit / UnknownCurrentWorld same-world: refresh flag only; keep the
	/// in-flight destination world. No-op when empty (lost race with flush take).
	/// </summary>
	public static void RefreshFlagKeepWorld(ref HuntAlertsPendingDefer? slot, HuntFlag flag)
	{
		while (true)
		{
			var cur = Volatile.Read(ref slot);
			if (cur == null)
				return;

			var next = new HuntAlertsPendingDefer(flag, cur.World);
			if (Interlocked.CompareExchange(ref slot, next, cur) == cur)
				return;
		}
	}

	public static void Clear(ref HuntAlertsPendingDefer? slot)
		=> Interlocked.Exchange(ref slot, null);

	/// <summary>
	/// Atomically take pending when <paramref name="currentWorldName"/> matches the
	/// stashed world. Returns null when empty, not yet on world, or a newer slot
	/// replaced this one (newer left in place — not dropped).
	/// </summary>
	public static HuntAlertsPendingDefer? TryTakeForFlush(
		ref HuntAlertsPendingDefer? slot,
		string? currentWorldName)
	{
		var cur = Volatile.Read(ref slot);
		if (cur == null)
			return null;

		if (!HuntAlertsPipelineIntake.ShouldFlushDeferred(cur.World, currentWorldName))
			return null;

		if (Interlocked.CompareExchange(ref slot, null, cur) != cur)
			return null;

		return cur;
	}

	/// <summary>
	/// After a failed Accept following <see cref="TryTakeForFlush"/>, put
	/// <paramref name="taken"/> back only if the slot is still empty (CAS).
	/// Returns false when a newer Store already occupies the slot (leave newer).
	/// </summary>
	public static bool TryRestoreIfEmpty(
		ref HuntAlertsPendingDefer? slot,
		HuntAlertsPendingDefer taken)
	{
		ArgumentNullException.ThrowIfNull(taken);
		return Interlocked.CompareExchange(ref slot, taken, null) == null;
	}
}
