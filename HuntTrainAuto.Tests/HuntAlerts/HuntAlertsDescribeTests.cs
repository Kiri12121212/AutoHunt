#nullable enable

using HuntTrainAuto.Domain;
using HuntTrainAuto.HuntAlerts;

namespace HuntTrainAuto.Tests.HuntAlerts;

public sealed class HuntAlertsDescribeTests
{
	[Fact]
	public void Conductor_describe_covers_added_and_disabled()
	{
		Assert.Equal(
			"added conductor 'Nelumy Teishi' @Twintania",
			HuntAlertsConductorDecision.Describe(
				new(HuntAlertsConductorAssignKind.Added, "Nelumy Teishi", "Twintania")));
		Assert.Equal(
			"auto-conductor disabled",
			HuntAlertsConductorDecision.Describe(
				new(HuntAlertsConductorAssignKind.Disabled, null, null)));
	}

	[Fact]
	public void Filter_describe_explains_accept_and_reject()
	{
		Assert.Equal(
			"rejected: integration off",
			HuntAlertsFilter.DescribeAcceptance(false, null, null, HuntMarkRank.A, "Phoenix"));
		Assert.Equal(
			"accepted: rank=A world=Phoenix",
			HuntAlertsFilter.DescribeAcceptance(true, null, null, HuntMarkRank.A, "Phoenix"));
	}

	[Fact]
	public void Pipeline_and_world_visit_describe_outcomes()
	{
		Assert.Equal("defer until on hunt world",
			HuntAlertsPipelineIntake.Describe(HuntAlertsPipelineIntakeKind.DeferUntilOnWorld));
		Assert.Equal("skip conflicting busy defer",
			HuntAlertsPipelineIntake.Describe(HuntAlertsBusyDeferKind.SkipConflict));
		Assert.Equal(
			"request world visit world=Phoenix",
			HuntAlertsWorldVisitDecision.Describe(new()
			{
				Action = HuntAlertsWorldVisitAction.RequestWorldVisit,
				World = "Phoenix",
			}));
	}

	[Fact]
	public void Mapper_dedupe_and_side_effect_descriptions_are_stable()
	{
		Assert.Equal(
			"mapping rejected: missing map coords",
			HuntTrainMessageMapper.DescribeRejectReason(" missing map coords "));
		Assert.Equal(
			"dedupe suppressed: cross-source",
			HuntAlertsFlagDedupe.Describe(true, "cross-source"));
		Assert.Equal(
			"cleared untrusted IPC arrival; retained instance=2",
			HuntAlertsArrivalTrust.Describe(2));
		Assert.Equal(
			"stored deferred flag world=Phoenix",
			HuntAlertsPendingDeferSlot.Describe("stored deferred flag", "Phoenix"));
		Assert.Equal(
			"cleared queued flags count=2",
			HuntAlertsFlagQueue.Describe("cleared queued flags", 2));
	}
}
