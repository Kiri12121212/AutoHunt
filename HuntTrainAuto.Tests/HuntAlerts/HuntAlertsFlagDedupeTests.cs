#nullable enable

using System;
using HuntTrainAuto.Domain;

namespace HuntTrainAuto.Tests.HuntAlerts;

public sealed class HuntAlertsFlagDedupeTests
{
	private static HuntFlag Flag(uint territory, int rawX, int rawY)
		=> HuntFlag.FromMapLink(territory, 1, rawX, rawY, "x", DateTimeOffset.UnixEpoch);

	[Fact]
	public void IsNearDuplicate_false_when_no_active()
		=> Assert.False(HuntAlertsFlagDedupe.IsNearDuplicate(null, Flag(813, 0, 0)));

	[Fact]
	public void IsNearDuplicate_false_when_different_territory()
	{
		var active = Flag(813, 0, 0);
		Assert.False(HuntAlertsFlagDedupe.IsNearDuplicate(active, Flag(814, 0, 0)));
	}

	[Fact]
	public void IsNearDuplicate_true_when_within_threshold()
	{
		// Scaled distance: |5000|/1000 = 5 < 10
		var active = Flag(813, 0, 0);
		Assert.True(HuntAlertsFlagDedupe.IsNearDuplicate(active, Flag(813, 5000, 0)));
	}

	[Fact]
	public void IsNearDuplicate_false_when_beyond_threshold()
	{
		// Scaled distance: 15000/1000 = 15 > 10
		var active = Flag(813, 0, 0);
		Assert.False(HuntAlertsFlagDedupe.IsNearDuplicate(active, Flag(813, 15_000, 0)));
	}

	[Fact]
	public void ShouldSuppress_only_when_pipeline_active_and_near_dup()
	{
		var active = Flag(813, 0, 0);
		var near = Flag(813, 1000, 0);
		var far = Flag(813, 20_000, 0);

		Assert.True(HuntAlertsFlagDedupe.ShouldSuppress(active, near, pipelineActive: true));
		Assert.False(HuntAlertsFlagDedupe.ShouldSuppress(active, near, pipelineActive: false));
		Assert.False(HuntAlertsFlagDedupe.ShouldSuppress(active, far, pipelineActive: true));
	}

	[Fact]
	public void ShouldSuppress_false_when_forceAccept_even_if_near_dup_pipeline_active()
	{
		var active = Flag(813, 0, 0);
		var near = Flag(813, 1000, 0);

		Assert.True(HuntAlertsFlagDedupe.ShouldSuppress(active, near, pipelineActive: true));
		Assert.False(HuntAlertsFlagDedupe.ShouldSuppress(
			active, near, pipelineActive: true, forceAccept: true));
	}

	[Fact]
	public void ShouldProceedAbortVisitThenEnter_false_when_near_dup_would_suppress()
	{
		var active = Flag(813, 0, 0);
		var near = Flag(813, 1000, 0);

		Assert.False(HuntAlertsFlagDedupe.ShouldProceedAbortVisitThenEnter(
			active, near, pipelineActive: true));
	}

	[Fact]
	public void ShouldProceedAbortVisitThenEnter_true_when_accept_would_run()
	{
		var active = Flag(813, 0, 0);
		var far = Flag(813, 20_000, 0);
		var nearIdle = Flag(813, 1000, 0);

		Assert.True(HuntAlertsFlagDedupe.ShouldProceedAbortVisitThenEnter(
			active, far, pipelineActive: true));
		Assert.True(HuntAlertsFlagDedupe.ShouldProceedAbortVisitThenEnter(
			active, nearIdle, pipelineActive: false));
		Assert.True(HuntAlertsFlagDedupe.ShouldProceedAbortVisitThenEnter(
			null, nearIdle, pipelineActive: true));
	}
}
