#nullable enable

namespace HuntTrainAuto.Tests.HuntAlerts;

public sealed class HuntAlertsPipelineIntakeTests
{
	[Theory]
	[InlineData(HuntAlertsWorldVisitAction.SameWorld, HuntAlertsPipelineIntakeKind.EnterPipeline)]
	[InlineData(HuntAlertsWorldVisitAction.UnknownCurrentWorld, HuntAlertsPipelineIntakeKind.EnterPipeline)]
	[InlineData(HuntAlertsWorldVisitAction.RequestWorldVisit, HuntAlertsPipelineIntakeKind.DeferUntilOnWorld)]
	[InlineData(HuntAlertsWorldVisitAction.NoOp, HuntAlertsPipelineIntakeKind.Skip)]
	[InlineData(HuntAlertsWorldVisitAction.CannotVisit, HuntAlertsPipelineIntakeKind.Skip)]
	[InlineData(HuntAlertsWorldVisitAction.BusyMidVisit, HuntAlertsPipelineIntakeKind.Skip)]
	[InlineData(HuntAlertsWorldVisitAction.DeferReplaceFailed, HuntAlertsPipelineIntakeKind.DeferUntilOnWorld)]
	public void Decide_maps_visit_action(
		HuntAlertsWorldVisitAction visit,
		HuntAlertsPipelineIntakeKind expected)
		=> Assert.Equal(expected, HuntAlertsPipelineIntake.Decide(visit));

	[Fact]
	public void Decide_DeferReplaceFailed_stores_incoming_world_via_defer()
		// Abort + failed ChangeWorld: intake Defers so Plugin Stores incoming (newest retained).
		=> Assert.Equal(
			HuntAlertsPipelineIntakeKind.DeferUntilOnWorld,
			HuntAlertsPipelineIntake.Decide(
				HuntAlertsWorldVisitAction.DeferReplaceFailed,
				hasPendingDefer: true,
				pendingWorld: "Cerberus",
				newHuntWorld: "Phoenix"));

	[Fact]
	public void Decide_BusyMidVisit_same_pending_world_refreshes_defer()
		=> Assert.Equal(
			HuntAlertsPipelineIntakeKind.DeferUntilOnWorld,
			HuntAlertsPipelineIntake.Decide(
				HuntAlertsWorldVisitAction.BusyMidVisit,
				hasPendingDefer: true,
				pendingWorld: "Cerberus",
				newHuntWorld: "cerberus"));

	[Fact]
	public void Decide_BusyMidVisit_different_pending_world_skips()
		=> Assert.Equal(
			HuntAlertsPipelineIntakeKind.Skip,
			HuntAlertsPipelineIntake.Decide(
				HuntAlertsWorldVisitAction.BusyMidVisit,
				hasPendingDefer: true,
				pendingWorld: "Cerberus",
				newHuntWorld: "Phoenix"));

	[Fact]
	public void Decide_BusyMidVisit_without_pending_is_true_skip()
		=> Assert.Equal(
			HuntAlertsPipelineIntakeKind.Skip,
			HuntAlertsPipelineIntake.Decide(
				HuntAlertsWorldVisitAction.BusyMidVisit,
				hasPendingDefer: false));

	[Fact]
	public void Decide_UnknownCurrentWorld_no_pending_enters()
		=> Assert.Equal(
			HuntAlertsPipelineIntakeKind.EnterPipeline,
			HuntAlertsPipelineIntake.Decide(
				HuntAlertsWorldVisitAction.UnknownCurrentWorld,
				hasPendingDefer: false,
				pendingWorld: null,
				newHuntWorld: "Phoenix"));

	[Fact]
	public void Decide_UnknownCurrentWorld_same_pending_world_refreshes_defer()
		=> Assert.Equal(
			HuntAlertsPipelineIntakeKind.DeferUntilOnWorld,
			HuntAlertsPipelineIntake.Decide(
				HuntAlertsWorldVisitAction.UnknownCurrentWorld,
				hasPendingDefer: true,
				pendingWorld: "Cerberus",
				newHuntWorld: "cerberus"));

	[Fact]
	public void Decide_UnknownCurrentWorld_different_pending_world_skips()
		// Occupied pending + unreadable current: keep visit — do not EnterPipeline /
		// AbortVisitThenEnter (IsBusy may already be false during loading/transitions).
		=> Assert.Equal(
			HuntAlertsPipelineIntakeKind.Skip,
			HuntAlertsPipelineIntake.Decide(
				HuntAlertsWorldVisitAction.UnknownCurrentWorld,
				hasPendingDefer: true,
				pendingWorld: "Cerberus",
				newHuntWorld: "Phoenix"));

	[Fact]
	public void DecideBusyDeferRefresh_same_world_refreshes_flag_keeps_world()
		=> Assert.Equal(
			HuntAlertsBusyDeferKind.RefreshFlagKeepWorld,
			HuntAlertsPipelineIntake.DecideBusyDeferRefresh("Phoenix", "  phoenix "));

	[Fact]
	public void DecideBusyDeferRefresh_different_world_skips_conflict()
		=> Assert.Equal(
			HuntAlertsBusyDeferKind.SkipConflict,
			HuntAlertsPipelineIntake.DecideBusyDeferRefresh("Cerberus", "Phoenix"));

	[Fact]
	public void DecideBusyDeferRefresh_null_or_missing_is_conflict()
	{
		Assert.Equal(
			HuntAlertsBusyDeferKind.SkipConflict,
			HuntAlertsPipelineIntake.DecideBusyDeferRefresh(null, "Phoenix"));
		Assert.Equal(
			HuntAlertsBusyDeferKind.SkipConflict,
			HuntAlertsPipelineIntake.DecideBusyDeferRefresh("Phoenix", null));
	}

	[Fact]
	public void ShouldFlushDeferred_when_current_matches_pending()
	{
		Assert.True(HuntAlertsPipelineIntake.ShouldFlushDeferred("Phoenix", "phoenix"));
		Assert.True(HuntAlertsPipelineIntake.ShouldFlushDeferred("  Cerberus ", "Cerberus"));
	}

	[Fact]
	public void ShouldFlushDeferred_false_when_missing_or_different()
	{
		Assert.False(HuntAlertsPipelineIntake.ShouldFlushDeferred(null, "Phoenix"));
		Assert.False(HuntAlertsPipelineIntake.ShouldFlushDeferred("", "Phoenix"));
		Assert.False(HuntAlertsPipelineIntake.ShouldFlushDeferred("Phoenix", null));
		Assert.False(HuntAlertsPipelineIntake.ShouldFlushDeferred("Phoenix", "Cerberus"));
	}

	[Fact]
	public void ShouldRetryPendingChangeWorld_true_when_pending_idle_off_world()
		=> Assert.True(
			HuntAlertsPipelineIntake.ShouldRetryPendingChangeWorld(
				hasPendingDefer: true,
				pendingWorld: "Phoenix",
				currentWorldName: "Cerberus",
				lifestreamBusy: false));

	[Fact]
	public void ShouldRetryPendingChangeWorld_false_when_busy()
		=> Assert.False(
			HuntAlertsPipelineIntake.ShouldRetryPendingChangeWorld(
				hasPendingDefer: true,
				pendingWorld: "Phoenix",
				currentWorldName: "Cerberus",
				lifestreamBusy: true));

	[Fact]
	public void ShouldRetryPendingChangeWorld_false_when_already_on_pending_world()
		// Flush path — not ChangeWorld retry.
		=> Assert.False(
			HuntAlertsPipelineIntake.ShouldRetryPendingChangeWorld(
				hasPendingDefer: true,
				pendingWorld: "Phoenix",
				currentWorldName: "phoenix",
				lifestreamBusy: false));

	[Fact]
	public void ShouldRetryPendingChangeWorld_false_when_no_pending_or_unknown_current()
	{
		Assert.False(
			HuntAlertsPipelineIntake.ShouldRetryPendingChangeWorld(
				hasPendingDefer: false,
				pendingWorld: null,
				currentWorldName: "Cerberus",
				lifestreamBusy: false));
		Assert.False(
			HuntAlertsPipelineIntake.ShouldRetryPendingChangeWorld(
				hasPendingDefer: true,
				pendingWorld: "Phoenix",
				currentWorldName: null,
				lifestreamBusy: false));
		Assert.False(
			HuntAlertsPipelineIntake.ShouldRetryPendingChangeWorld(
				hasPendingDefer: true,
				pendingWorld: "",
				currentWorldName: "Cerberus",
				lifestreamBusy: false));
	}

	[Fact]
	public void ShouldRetryPendingChangeWorld_false_when_visit_just_queued_this_tick()
		// Any Process ChangeWorld attempt (success or BusyMidVisit refresh fail) sets
		// skipHaPendingChangeWorldRetryThisTick — soft-retry must not re-issue same tick.
		=> Assert.False(
			HuntAlertsPipelineIntake.ShouldRetryPendingChangeWorld(
				hasPendingDefer: true,
				pendingWorld: "Phoenix",
				currentWorldName: "Cerberus",
				lifestreamBusy: false,
				visitJustQueuedThisTick: true));

	[Fact]
	public void DeferredStashReplacesPrior_newest_wins_single_slot()
	{
		Assert.False(HuntAlertsPipelineIntake.DeferredStashReplacesPrior(hasPendingFlag: false));
		Assert.True(HuntAlertsPipelineIntake.DeferredStashReplacesPrior(hasPendingFlag: true));
	}

	[Fact]
	public void DecideEnterWithPending_no_pending_enters()
		=> Assert.Equal(
			HuntAlertsEnterWithPendingKind.Enter,
			HuntAlertsPipelineIntake.DecideEnterWithPending(
				hasPendingDefer: false,
				pendingWorld: null,
				newHuntWorld: "Phoenix"));

	[Fact]
	public void DecideEnterWithPending_same_pending_world_keeps_defer()
		=> Assert.Equal(
			HuntAlertsEnterWithPendingKind.ReplacePendingKeepDefer,
			HuntAlertsPipelineIntake.DecideEnterWithPending(
				hasPendingDefer: true,
				pendingWorld: "Cerberus",
				newHuntWorld: "cerberus"));

	[Fact]
	public void DecideEnterWithPending_different_world_aborts_then_enters()
		=> Assert.Equal(
			HuntAlertsEnterWithPendingKind.AbortVisitThenEnter,
			HuntAlertsPipelineIntake.DecideEnterWithPending(
				hasPendingDefer: true,
				pendingWorld: "Cerberus",
				newHuntWorld: "Phoenix"));

	[Fact]
	public void DecideEnterWithPending_pending_with_null_new_world_aborts_then_enters()
		=> Assert.Equal(
			HuntAlertsEnterWithPendingKind.AbortVisitThenEnter,
			HuntAlertsPipelineIntake.DecideEnterWithPending(
				hasPendingDefer: true,
				pendingWorld: "Cerberus",
				newHuntWorld: null));

	[Fact]
	public void ShouldAbortPriorVisitOnDeferReplace_no_pending_false()
		=> Assert.False(
			HuntAlertsPipelineIntake.ShouldAbortPriorVisitOnDeferReplace(
				hasPendingDefer: false,
				pendingWorld: null,
				newHuntWorld: "Phoenix"));

	[Fact]
	public void ShouldAbortPriorVisitOnDeferReplace_same_world_false()
		=> Assert.False(
			HuntAlertsPipelineIntake.ShouldAbortPriorVisitOnDeferReplace(
				hasPendingDefer: true,
				pendingWorld: "Cerberus",
				newHuntWorld: "  cerberus "));

	[Fact]
	public void ShouldAbortPriorVisitOnDeferReplace_different_world_true()
		=> Assert.True(
			HuntAlertsPipelineIntake.ShouldAbortPriorVisitOnDeferReplace(
				hasPendingDefer: true,
				pendingWorld: "Cerberus",
				newHuntWorld: "Phoenix"));

	[Fact]
	public void ShouldAbortPriorVisitOnDeferReplace_null_new_world_true()
		=> Assert.True(
			HuntAlertsPipelineIntake.ShouldAbortPriorVisitOnDeferReplace(
				hasPendingDefer: true,
				pendingWorld: "Cerberus",
				newHuntWorld: null));
}
