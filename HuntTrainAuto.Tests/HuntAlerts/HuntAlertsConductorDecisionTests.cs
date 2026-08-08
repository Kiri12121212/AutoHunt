#nullable enable

using System.Collections.Generic;

namespace HuntTrainAuto.Tests.HuntAlerts;

public sealed class HuntAlertsConductorDecisionTests
{
	[Fact]
	public void Decide_adds_parsed_name()
	{
		var conductors = new List<string>();
		var result = HuntAlertsConductorDecision.Decide(
			conductors,
			"Conductor: [Twintania] Nelumy Teishi");

		Assert.Equal(HuntAlertsConductorAssignKind.Added, result.Kind);
		Assert.Equal("Nelumy Teishi", result.Name);
		Assert.Equal("Twintania", result.World);
		Assert.Equal(new[] { "Nelumy Teishi" }, conductors);
	}

	[Fact]
	public void Decide_already_present_case_insensitive()
	{
		var conductors = new List<string> { "Nelumy Teishi" };
		var result = HuntAlertsConductorDecision.Decide(
			conductors,
			"Conductor - nelumy teishi");

		Assert.Equal(HuntAlertsConductorAssignKind.AlreadyPresent, result.Kind);
		Assert.Equal("nelumy teishi", result.Name);
		Assert.Single(conductors);
	}

	[Fact]
	public void Decide_no_name_leaves_list_unchanged()
	{
		var conductors = new List<string> { "Existing" };
		var result = HuntAlertsConductorDecision.Decide(conductors, "Hunt train starting soon");

		Assert.Equal(HuntAlertsConductorAssignKind.NoName, result.Kind);
		Assert.Null(result.Name);
		Assert.Equal(new[] { "Existing" }, conductors);
	}

	[Fact]
	public void Decide_null_message_is_no_name()
	{
		var conductors = new List<string>();
		var result = HuntAlertsConductorDecision.Decide(conductors, null);

		Assert.Equal(HuntAlertsConductorAssignKind.NoName, result.Kind);
		Assert.Empty(conductors);
	}

	[Fact]
	public void Decide_disabled_skips_parse_and_add()
	{
		var conductors = new List<string>();
		var result = HuntAlertsConductorDecision.Decide(
			conductors,
			"Conductor - Attacker Name",
			enabled: false);

		Assert.Equal(HuntAlertsConductorAssignKind.Disabled, result.Kind);
		Assert.Empty(conductors);
	}
}
