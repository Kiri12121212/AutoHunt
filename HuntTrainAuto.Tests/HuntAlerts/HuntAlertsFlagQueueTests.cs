#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using HuntTrainAuto.Domain;
using HuntTrainAuto.HuntAlerts;

namespace HuntTrainAuto.Tests.HuntAlerts;

public sealed class HuntAlertsFlagQueueTests
{
	[Fact]
	public void DequeueBatch_empties_queue_preserving_order()
	{
		var queue = new ConcurrentQueue<HuntFlag>();
		var a = HuntFlag.FromMapLink(813, 1, 0, 0, "a", DateTimeOffset.UnixEpoch);
		var b = HuntFlag.FromMapLink(813, 1, 1, 1, "b", DateTimeOffset.UnixEpoch);
		HuntAlertsFlagQueue.Enqueue(queue, a);
		HuntAlertsFlagQueue.Enqueue(queue, b);

		var batch = HuntAlertsFlagQueue.DequeueBatch(queue);

		Assert.Equal(new[] { a, b }, batch);
		Assert.True(queue.IsEmpty);
	}

	[Fact]
	public void SelectNewestForTick_returns_last_or_null()
	{
		Assert.Null(HuntAlertsFlagQueue.SelectNewestForTick(Array.Empty<HuntFlag>()));

		var a = HuntFlag.FromMapLink(813, 1, 0, 0, "a", DateTimeOffset.UnixEpoch);
		var b = HuntFlag.FromMapLink(813, 1, 1, 1, "b", DateTimeOffset.UnixEpoch);
		Assert.Same(a, HuntAlertsFlagQueue.SelectNewestForTick(new[] { a }));
		Assert.Same(b, HuntAlertsFlagQueue.SelectNewestForTick(new[] { a, b }));
	}

	[Fact]
	public void Drain_processes_only_newest_in_batch()
	{
		// Same-tick Accept then RequestWorldVisit must not both run (active + pending).
		var queue = new ConcurrentQueue<HuntFlag>();
		var a = HuntFlag.FromMapLink(813, 1, 0, 0, "a", DateTimeOffset.UnixEpoch);
		var b = HuntFlag.FromMapLink(813, 1, 1, 1, "b", DateTimeOffset.UnixEpoch);
		HuntAlertsFlagQueue.Enqueue(queue, a);
		HuntAlertsFlagQueue.Enqueue(queue, b);

		var seen = new List<HuntFlag>();
		HuntAlertsFlagQueue.Drain(queue, seen.Add);

		Assert.Equal(new[] { b }, seen);
		Assert.True(queue.IsEmpty);
	}

	[Fact]
	public void Drain_single_flag_is_handled()
	{
		var queue = new ConcurrentQueue<HuntFlag>();
		var a = HuntFlag.FromMapLink(813, 1, 0, 0, "a", DateTimeOffset.UnixEpoch);
		HuntAlertsFlagQueue.Enqueue(queue, a);

		var seen = new List<HuntFlag>();
		HuntAlertsFlagQueue.Drain(queue, seen.Add);

		Assert.Equal(new[] { a }, seen);
	}

	[Fact]
	public void Drain_soft_fails_when_newest_handler_throws()
	{
		var queue = new ConcurrentQueue<HuntFlag>();
		var a = HuntFlag.FromMapLink(813, 1, 0, 0, "a", DateTimeOffset.UnixEpoch);
		var b = HuntFlag.FromMapLink(813, 1, 1, 1, "b", DateTimeOffset.UnixEpoch);
		HuntAlertsFlagQueue.Enqueue(queue, a);
		HuntAlertsFlagQueue.Enqueue(queue, b);

		var calls = 0;
		HuntAlertsFlagQueue.Drain(queue, _ =>
		{
			calls++;
			throw new InvalidOperationException("boom");
		});

		Assert.Equal(1, calls);
		Assert.True(queue.IsEmpty);
	}

	[Fact]
	public void Drain_empty_is_noop()
	{
		var queue = new ConcurrentQueue<HuntFlag>();
		var calls = 0;
		HuntAlertsFlagQueue.Drain(queue, _ => calls++);
		Assert.Equal(0, calls);
	}

	[Fact]
	public void Clear_empties_all_without_handling()
	{
		var queue = new ConcurrentQueue<HuntFlag>();
		var a = HuntFlag.FromMapLink(813, 1, 0, 0, "a", DateTimeOffset.UnixEpoch);
		var b = HuntFlag.FromMapLink(813, 1, 1, 1, "b", DateTimeOffset.UnixEpoch);
		HuntAlertsFlagQueue.Enqueue(queue, a);
		HuntAlertsFlagQueue.Enqueue(queue, b);

		HuntAlertsFlagQueue.Clear(queue);

		Assert.True(queue.IsEmpty);
		var calls = 0;
		HuntAlertsFlagQueue.Drain(queue, _ => calls++);
		Assert.Equal(0, calls);
	}

	[Fact]
	public void Clear_empty_is_noop()
	{
		var queue = new ConcurrentQueue<HuntFlag>();
		HuntAlertsFlagQueue.Clear(queue);
		Assert.True(queue.IsEmpty);
	}

	[Fact]
	public void ClearQueueOnFrameworkDrainAccept_is_false()
		=> Assert.False(HuntAlertsFlagQueue.ClearQueueOnFrameworkDrainAccept);

	[Fact]
	public void ShouldDrainThisTick_false_when_suppress_set()
	{
		Assert.True(HuntAlertsFlagQueue.ShouldDrainThisTick(suppressHaDrainRemainingThisTick: false));
		Assert.False(HuntAlertsFlagQueue.ShouldDrainThisTick(suppressHaDrainRemainingThisTick: true));
	}

	[Fact]
	public void BeginFrameworkTick_consumes_suppress_and_skips_drain()
	{
		// Chat Clear mid-tick → suppress; Drain later same tick (or next begin) skips once.
		var suppress = HuntAlertsFlagQueue.SuppressDrainAfterChatClear;
		Assert.True(suppress);
		Assert.False(HuntAlertsFlagQueue.BeginFrameworkTick(ref suppress));
		Assert.False(suppress);
		// Following tick drains normally.
		Assert.True(HuntAlertsFlagQueue.BeginFrameworkTick(ref suppress));
		Assert.False(suppress);
	}

	[Fact]
	public void BeginFrameworkTick_allows_drain_when_not_suppressed()
	{
		var suppress = false;
		Assert.True(HuntAlertsFlagQueue.BeginFrameworkTick(ref suppress));
		Assert.False(suppress);
	}

	[Fact]
	public void BeginFrameworkTick_clears_skip_pending_ChangeWorld_retry()
	{
		var suppress = false;
		var skipRetry = true;
		Assert.True(HuntAlertsFlagQueue.BeginFrameworkTick(ref suppress, ref skipRetry));
		Assert.False(suppress);
		Assert.False(skipRetry);
	}

	[Fact]
	public void ShouldAbortLifestreamOnAdopt_only_when_clearing_pending()
	{
		// Chat / shared clear: Abort. HA drain Accept / flush: do not Abort in-flight visit.
		Assert.True(HuntAlertsFlagQueue.ShouldAbortLifestreamOnAdopt(clearPendingDefer: true));
		Assert.False(HuntAlertsFlagQueue.ShouldAbortLifestreamOnAdopt(clearPendingDefer: false));
	}
}
