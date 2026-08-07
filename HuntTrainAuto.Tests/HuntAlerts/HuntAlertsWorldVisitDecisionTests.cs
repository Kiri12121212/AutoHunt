#nullable enable

namespace HuntTrainAuto.Tests.HuntAlerts;

public sealed class HuntAlertsWorldVisitDecisionTests
{
	[Fact]
	public void Decide_no_op_when_integration_disabled()
	{
		var result = HuntAlertsWorldVisitDecision.Decide(
			huntAlertsIntegration: false,
			lifestreamAvailable: true,
			lifestreamBusy: false,
			currentWorldName: "Phoenix",
			arrivalWorld: "Cerberus",
			canVisitSameDc: true,
			canVisitCrossDc: false);

		Assert.Equal(HuntAlertsWorldVisitAction.NoOp, result.Action);
		Assert.Null(result.World);
	}

	[Fact]
	public void Decide_same_world_without_lifestream()
	{
		var result = HuntAlertsWorldVisitDecision.Decide(
			huntAlertsIntegration: true,
			lifestreamAvailable: false,
			lifestreamBusy: false,
			currentWorldName: "Phoenix",
			arrivalWorld: "Phoenix",
			canVisitSameDc: false,
			canVisitCrossDc: false);

		Assert.Equal(HuntAlertsWorldVisitAction.SameWorld, result.Action);
		Assert.Equal("Phoenix", result.World);
	}

	[Theory]
	[InlineData("Phoenix", "phoenix")]
	[InlineData("  Cerberus ", "Cerberus")]
	public void Decide_same_world_case_insensitive(string current, string arrival)
	{
		var result = HuntAlertsWorldVisitDecision.Decide(
			huntAlertsIntegration: true,
			lifestreamAvailable: true,
			lifestreamBusy: false,
			currentWorldName: current,
			arrivalWorld: arrival,
			canVisitSameDc: true,
			canVisitCrossDc: false);

		Assert.Equal(HuntAlertsWorldVisitAction.SameWorld, result.Action);
	}

	[Fact]
	public void Decide_cross_world_requests_visit_when_same_dc()
	{
		var result = HuntAlertsWorldVisitDecision.Decide(
			huntAlertsIntegration: true,
			lifestreamAvailable: true,
			lifestreamBusy: false,
			currentWorldName: "Phoenix",
			arrivalWorld: "Cerberus",
			canVisitSameDc: true,
			canVisitCrossDc: false);

		Assert.Equal(HuntAlertsWorldVisitAction.RequestWorldVisit, result.Action);
		Assert.Equal("Cerberus", result.World);
	}

	[Fact]
	public void Decide_cross_world_requests_visit_when_cross_dc()
	{
		var result = HuntAlertsWorldVisitDecision.Decide(
			huntAlertsIntegration: true,
			lifestreamAvailable: true,
			lifestreamBusy: false,
			currentWorldName: "Phoenix",
			arrivalWorld: "Gilgamesh",
			canVisitSameDc: false,
			canVisitCrossDc: true);

		Assert.Equal(HuntAlertsWorldVisitAction.RequestWorldVisit, result.Action);
		Assert.Equal("Gilgamesh", result.World);
	}

	[Fact]
	public void Decide_no_op_when_lifestream_missing_cross_world()
	{
		var result = HuntAlertsWorldVisitDecision.Decide(
			huntAlertsIntegration: true,
			lifestreamAvailable: false,
			lifestreamBusy: false,
			currentWorldName: "Phoenix",
			arrivalWorld: "Cerberus",
			canVisitSameDc: true,
			canVisitCrossDc: false);

		Assert.Equal(HuntAlertsWorldVisitAction.NoOp, result.Action);
		Assert.Null(result.World);
	}

	[Fact]
	public void Decide_busy_mid_visit_when_lifestream_busy()
	{
		var result = HuntAlertsWorldVisitDecision.Decide(
			huntAlertsIntegration: true,
			lifestreamAvailable: true,
			lifestreamBusy: true,
			currentWorldName: "Phoenix",
			arrivalWorld: "Cerberus",
			canVisitSameDc: true,
			canVisitCrossDc: false);

		Assert.Equal(HuntAlertsWorldVisitAction.BusyMidVisit, result.Action);
		Assert.Equal("Cerberus", result.World);
	}

	[Fact]
	public void Decide_busy_mid_visit_keeps_world_even_when_not_whitelisted()
	{
		var result = HuntAlertsWorldVisitDecision.Decide(
			huntAlertsIntegration: true,
			lifestreamAvailable: true,
			lifestreamBusy: true,
			currentWorldName: "Phoenix",
			arrivalWorld: "FakeWorld",
			canVisitSameDc: false,
			canVisitCrossDc: false);

		Assert.Equal(HuntAlertsWorldVisitAction.BusyMidVisit, result.Action);
		Assert.Equal("FakeWorld", result.World);
	}

	[Fact]
	public void Decide_cannot_visit_when_not_whitelisted()
	{
		var result = HuntAlertsWorldVisitDecision.Decide(
			huntAlertsIntegration: true,
			lifestreamAvailable: true,
			lifestreamBusy: false,
			currentWorldName: "Phoenix",
			arrivalWorld: "FakeWorld",
			canVisitSameDc: false,
			canVisitCrossDc: false);

		Assert.Equal(HuntAlertsWorldVisitAction.CannotVisit, result.Action);
		Assert.Equal("FakeWorld", result.World);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void Decide_unknown_current_world_does_not_request_visit(string? current)
	{
		var result = HuntAlertsWorldVisitDecision.Decide(
			huntAlertsIntegration: true,
			lifestreamAvailable: true,
			lifestreamBusy: false,
			currentWorldName: current,
			arrivalWorld: "Cerberus",
			canVisitSameDc: true,
			canVisitCrossDc: false);

		Assert.Equal(HuntAlertsWorldVisitAction.UnknownCurrentWorld, result.Action);
		Assert.Equal("Cerberus", result.World);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void Decide_busy_unknown_current_prefers_BusyMidVisit(string? current)
	{
		// Mid-visit + unreadable current must not become UnknownCurrentWorld →
		// EnterPipeline / AbortVisitThenEnter; intake compares against pending.
		var result = HuntAlertsWorldVisitDecision.Decide(
			huntAlertsIntegration: true,
			lifestreamAvailable: true,
			lifestreamBusy: true,
			currentWorldName: current,
			arrivalWorld: "Cerberus",
			canVisitSameDc: true,
			canVisitCrossDc: false);

		Assert.Equal(HuntAlertsWorldVisitAction.BusyMidVisit, result.Action);
		Assert.Equal("Cerberus", result.World);
	}

	[Fact]
	public void Decide_busy_unknown_current_without_lifestream_is_UnknownCurrentWorld()
	{
		var result = HuntAlertsWorldVisitDecision.Decide(
			huntAlertsIntegration: true,
			lifestreamAvailable: false,
			lifestreamBusy: true,
			currentWorldName: null,
			arrivalWorld: "Cerberus",
			canVisitSameDc: true,
			canVisitCrossDc: false);

		Assert.Equal(HuntAlertsWorldVisitAction.UnknownCurrentWorld, result.Action);
		Assert.Equal("Cerberus", result.World);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("bad;world")]
	[InlineData("evil\nworld")]
	[InlineData("../../etc")]
	public void Decide_no_op_on_invalid_arrival_world(string? arrival)
	{
		var result = HuntAlertsWorldVisitDecision.Decide(
			huntAlertsIntegration: true,
			lifestreamAvailable: true,
			lifestreamBusy: false,
			currentWorldName: "Phoenix",
			arrivalWorld: arrival,
			canVisitSameDc: true,
			canVisitCrossDc: true);

		Assert.Equal(HuntAlertsWorldVisitAction.NoOp, result.Action);
	}

	[Theory]
	[InlineData("Phoenix", true)]
	[InlineData("  Spriggan ", true)]
	[InlineData("Odin's", true)]
	[InlineData("A-World", true)]
	[InlineData("", false)]
	[InlineData("bad;cmd", false)]
	[InlineData("a/b", false)]
	public void TrySanitizeWorldName(string? input, bool ok)
	{
		Assert.Equal(ok, HuntAlertsWorldVisitDecision.TrySanitizeWorldName(input, out var sanitized));
		if (ok)
			Assert.Equal(input!.Trim(), sanitized);
	}

	[Fact]
	public void IsSameWorld_false_when_either_blank()
	{
		Assert.False(HuntAlertsWorldVisitDecision.IsSameWorld(null, "Phoenix"));
		Assert.False(HuntAlertsWorldVisitDecision.IsSameWorld("Phoenix", null));
		Assert.False(HuntAlertsWorldVisitDecision.IsSameWorld("", "Phoenix"));
	}
}
