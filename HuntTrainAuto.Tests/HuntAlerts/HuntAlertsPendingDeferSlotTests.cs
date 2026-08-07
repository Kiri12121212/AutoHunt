#nullable enable

using System;
using HuntTrainAuto.Domain;
using HuntTrainAuto.HuntAlerts;

namespace HuntTrainAuto.Tests.HuntAlerts;

public sealed class HuntAlertsPendingDeferSlotTests
{
	[Fact]
	public void TryTakeForFlush_takes_when_world_matches()
	{
		HuntAlertsPendingDefer? slot = null;
		var flag = HuntFlag.FromMapLink(813, 1, 0, 0, "a", DateTimeOffset.UnixEpoch);
		HuntAlertsPendingDeferSlot.Store(ref slot, flag, "Cerberus");

		var taken = HuntAlertsPendingDeferSlot.TryTakeForFlush(ref slot, "cerberus");
		Assert.NotNull(taken);
		Assert.Same(flag, taken.Flag);
		Assert.Null(slot);
	}

	[Fact]
	public void TryTakeForFlush_leaves_slot_when_world_differs()
	{
		HuntAlertsPendingDefer? slot = null;
		var flag = HuntFlag.FromMapLink(813, 1, 0, 0, "a", DateTimeOffset.UnixEpoch);
		HuntAlertsPendingDeferSlot.Store(ref slot, flag, "Cerberus");

		Assert.Null(HuntAlertsPendingDeferSlot.TryTakeForFlush(ref slot, "Phoenix"));
		Assert.NotNull(slot);
		Assert.Equal("Cerberus", slot.World);
	}

	[Fact]
	public void TryTakeForFlush_does_not_drop_newer_store()
	{
		HuntAlertsPendingDefer? slot = null;
		var older = HuntFlag.FromMapLink(813, 1, 0, 0, "old", DateTimeOffset.UnixEpoch);
		var newer = HuntFlag.FromMapLink(813, 1, 10, 10, "new", DateTimeOffset.UnixEpoch);
		HuntAlertsPendingDeferSlot.Store(ref slot, older, "Cerberus");

		// Simulate flush snapshot of older, then a newer Store before CAS clear.
		var snapshot = slot;
		HuntAlertsPendingDeferSlot.Store(ref slot, newer, "Cerberus");
		Assert.NotSame(snapshot, slot);

		// CAS against stale snapshot fails — newer remains.
		Assert.False(
			ReferenceEquals(
				System.Threading.Interlocked.CompareExchange(ref slot, null, snapshot),
				snapshot));
		Assert.NotNull(slot);
		Assert.Same(newer, slot!.Flag);

		var taken = HuntAlertsPendingDeferSlot.TryTakeForFlush(ref slot, "Cerberus");
		Assert.NotNull(taken);
		Assert.Same(newer, taken.Flag);
		Assert.Null(slot);
	}

	[Fact]
	public void RefreshFlagKeepWorld_preserves_world()
	{
		HuntAlertsPendingDefer? slot = null;
		var first = HuntFlag.FromMapLink(813, 1, 0, 0, "a", DateTimeOffset.UnixEpoch);
		var second = HuntFlag.FromMapLink(813, 1, 5, 5, "b", DateTimeOffset.UnixEpoch);
		HuntAlertsPendingDeferSlot.Store(ref slot, first, "Phoenix");
		HuntAlertsPendingDeferSlot.RefreshFlagKeepWorld(ref slot, second);

		Assert.NotNull(slot);
		Assert.Same(second, slot.Flag);
		Assert.Equal("Phoenix", slot.World);
	}

	[Fact]
	public void Clear_empties_slot()
	{
		HuntAlertsPendingDefer? slot = null;
		var flag = HuntFlag.FromMapLink(813, 1, 0, 0, "a", DateTimeOffset.UnixEpoch);
		HuntAlertsPendingDeferSlot.Store(ref slot, flag, "Phoenix");
		HuntAlertsPendingDeferSlot.Clear(ref slot);
		Assert.Null(slot);
	}

	[Fact]
	public void Store_replaces_prior_world_retains_incoming_on_DeferReplaceFailed()
	{
		// Soft-clear aborted prior by Store of incoming (never empty-clear after Abort).
		HuntAlertsPendingDefer? slot = null;
		var prior = HuntFlag.FromMapLink(813, 1, 0, 0, "prior", DateTimeOffset.UnixEpoch);
		var incoming = HuntFlag.FromMapLink(813, 1, 10, 10, "incoming", DateTimeOffset.UnixEpoch);
		HuntAlertsPendingDeferSlot.Store(ref slot, prior, "Cerberus");
		HuntAlertsPendingDeferSlot.Store(ref slot, incoming, "Phoenix");

		Assert.NotNull(slot);
		Assert.Same(incoming, slot.Flag);
		Assert.Equal("Phoenix", slot.World);
	}

	[Fact]
	public void TryRestoreIfEmpty_restores_when_slot_empty()
	{
		HuntAlertsPendingDefer? slot = null;
		var flag = HuntFlag.FromMapLink(813, 1, 0, 0, "a", DateTimeOffset.UnixEpoch);
		HuntAlertsPendingDeferSlot.Store(ref slot, flag, "Cerberus");
		var taken = HuntAlertsPendingDeferSlot.TryTakeForFlush(ref slot, "Cerberus");
		Assert.NotNull(taken);
		Assert.Null(slot);

		Assert.True(HuntAlertsPendingDeferSlot.TryRestoreIfEmpty(ref slot, taken));
		Assert.Same(taken, slot);
		Assert.Same(flag, slot!.Flag);
	}

	[Fact]
	public void TryRestoreIfEmpty_leaves_newer_store()
	{
		HuntAlertsPendingDefer? slot = null;
		var older = HuntFlag.FromMapLink(813, 1, 0, 0, "old", DateTimeOffset.UnixEpoch);
		var newer = HuntFlag.FromMapLink(813, 1, 10, 10, "new", DateTimeOffset.UnixEpoch);
		HuntAlertsPendingDeferSlot.Store(ref slot, older, "Cerberus");
		var taken = HuntAlertsPendingDeferSlot.TryTakeForFlush(ref slot, "Cerberus");
		Assert.NotNull(taken);

		HuntAlertsPendingDeferSlot.Store(ref slot, newer, "Phoenix");
		Assert.False(HuntAlertsPendingDeferSlot.TryRestoreIfEmpty(ref slot, taken));
		Assert.Same(newer, slot!.Flag);
		Assert.Equal("Phoenix", slot.World);
	}
}
