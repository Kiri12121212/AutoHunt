#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using HuntTrainAuto.Domain;

namespace HuntTrainAuto.HuntAlerts;

/// <summary>
/// Marshals HuntAlerts mapped flags from the IPC callback onto
/// <c>Framework.Update</c> (TASKS 10.5). Soft-fail handle: one bad flag must not
/// throw out of the Framework tick. <see cref="Clear"/> drops without handle when
/// chat / master-off / dispose win over concurrent HA — never from Framework HA
/// drain Accept/Adopt (that would drop later flags enqueued during the tick and
/// break newest-wins).
/// <para>
/// Drain policy (option A): dequeue the whole batch, then handle only the
/// <em>newest</em> flag. FIFO handle-all would let an earlier same-world Accept
/// activate the train and a later <c>RequestWorldVisit</c> in the same tick still
/// <c>ChangeWorld</c>+Store → active hunt + conflicting pending.
/// </para>
/// </summary>
public static class HuntAlertsFlagQueue
{
	/// <summary>Compact, log-safe queue action summary.</summary>
	public static string Describe(string action, int count = 0)
		=> count > 0 ? $"{action} count={count}" : action;

	/// <summary>
	/// Whether Framework HA drain Accept/Adopt should Clear the IPC queue.
	/// Always false: Clear during handle drops flags enqueued later in the same tick.
	/// Chat adopt, master-off, and dispose still call <see cref="Clear"/>.
	/// </summary>
	public static bool ClearQueueOnFrameworkDrainAccept => false;

	/// <summary>
	/// Conductor-wins same-tick drain suppress (TASKS 10.5). After chat
	/// <see cref="Clear"/>, Plugin sets a flag so <see cref="Drain"/> later in the
	/// same Framework update is skipped — IPC enqueued after Clear must not
	/// ChangeWorld/Store/Accept and override the conductor adopt that tick.
	/// Consumed at the start of the next Framework update (see
	/// <see cref="BeginFrameworkTick"/>).
	/// </summary>
	public static bool ShouldDrainThisTick(bool suppressHaDrainRemainingThisTick)
		=> !suppressHaDrainRemainingThisTick;

	/// <summary>
	/// Start of Framework update: consume same-tick suppress; return whether Drain
	/// may run this tick. Clears the suppress flag so the following tick drains
	/// normally (post-chat IPC waits one tick max).
	/// </summary>
	public static bool BeginFrameworkTick(ref bool suppressHaDrainRemainingThisTick)
	{
		var unused = false;
		return BeginFrameworkTick(ref suppressHaDrainRemainingThisTick, ref unused);
	}

	/// <summary>
	/// Start of Framework update: consume drain suppress and clear
	/// <paramref name="skipPendingChangeWorldRetryThisTick"/> (set when Drain/Process
	/// attempted <c>ChangeWorld</c> — soft-retry must not re-issue same tick).
	/// </summary>
	public static bool BeginFrameworkTick(
		ref bool suppressHaDrainRemainingThisTick,
		ref bool skipPendingChangeWorldRetryThisTick)
	{
		var drain = ShouldDrainThisTick(suppressHaDrainRemainingThisTick);
		suppressHaDrainRemainingThisTick = false;
		skipPendingChangeWorldRetryThisTick = false;
		return drain;
	}

	/// <summary>
	/// After conductor chat Clear: set suppress so Drain later this Framework tick
	/// is skipped (conductor-wins).
	/// </summary>
	public static bool SuppressDrainAfterChatClear => true;

	/// <summary>
	/// Whether Adopt should soft-fail <c>Lifestream.Abort</c>. True only on the
	/// shared clear path (<c>clearPendingDefer</c> — chat / master-off style).
	/// Framework HA drain Accept and flush pass false so a visit just queued for
	/// pending is not cancelled; <c>AbortVisitThenEnter</c> Aborts on its own path.
	/// </summary>
	public static bool ShouldAbortLifestreamOnAdopt(bool clearPendingDefer)
		=> clearPendingDefer;

	/// <summary>Enqueue from the HuntAlerts CallGate callback (any thread).</summary>
	public static void Enqueue(
		ConcurrentQueue<HuntFlag> queue,
		HuntFlag flag,
		Action<string>? onDebug = null)
	{
		ArgumentNullException.ThrowIfNull(queue);
		ArgumentNullException.ThrowIfNull(flag);
		queue.Enqueue(flag);
		onDebug?.Invoke(Describe("enqueued mapped flag", queue.Count));
	}

	/// <summary>
	/// Drop all queued flags without handling. Used when conductor chat, master-off,
	/// or dispose wins over concurrent HuntAlerts so Framework drain cannot re-process
	/// stale IPC flags (ChangeWorld / Store → later flush overriding the adopt).
	/// Do not call from Framework HA drain Accept — see
	/// <see cref="ClearQueueOnFrameworkDrainAccept"/>.
	/// Soft-fail / best-effort: flags enqueued after this clear may still sit in the
	/// queue — Plugin must also suppress Drain for the remainder of the Framework tick
	/// (conductor-wins; see <see cref="SuppressDrainAfterChatClear"/>).
	/// </summary>
	public static void Clear(ConcurrentQueue<HuntFlag> queue, Action<string>? onDebug = null)
	{
		ArgumentNullException.ThrowIfNull(queue);
		var count = 0;
		while (queue.TryDequeue(out _))
			count++;
		if (count > 0)
			onDebug?.Invoke(Describe("cleared queued flags", count));
	}

	/// <summary>
	/// Dequeue every flag currently in the queue (snapshot for one Framework tick).
	/// Pure dequeue — no handle. Empty queue → empty list.
	/// </summary>
	public static List<HuntFlag> DequeueBatch(ConcurrentQueue<HuntFlag> queue)
	{
		ArgumentNullException.ThrowIfNull(queue);
		var batch = new List<HuntFlag>();
		while (queue.TryDequeue(out var flag))
			batch.Add(flag);
		return batch;
	}

	/// <summary>
	/// Newest-wins for one Framework tick: only the last flag in a drained batch
	/// is processed. Earlier batch siblings are discarded without handle.
	/// </summary>
	public static HuntFlag? SelectNewestForTick(IReadOnlyList<HuntFlag> batch)
	{
		ArgumentNullException.ThrowIfNull(batch);
		return batch.Count == 0 ? null : batch[^1];
	}

	/// <summary>
	/// Drain the queue on the Framework tick thread before pending-defer flush so
	/// Abort / ChangeWorld / Store / Take share one path. Dequeues the whole batch,
	/// then handles only <see cref="SelectNewestForTick"/> (newest wins for the tick).
	/// Handlers must not Clear this queue during handle.
	/// </summary>
	public static void Drain(
		ConcurrentQueue<HuntFlag> queue,
		Action<HuntFlag> handle,
		Action<string>? onDebug = null)
	{
		ArgumentNullException.ThrowIfNull(queue);
		ArgumentNullException.ThrowIfNull(handle);

		var batch = DequeueBatch(queue);
		var newest = SelectNewestForTick(batch);
		if (newest is null)
			return;

		try
		{
			handle(newest);
			onDebug?.Invoke(Describe(
				batch.Count == 1 ? "drained mapped flag" : "drained newest mapped flag; discarded older flags",
				batch.Count));
		}
		catch (Exception ex)
		{
			// Soft-fail: must not throw out of Framework.Update.
			onDebug?.Invoke($"drain handler soft-fail: {ex.GetType().Name}");
		}
	}
}
