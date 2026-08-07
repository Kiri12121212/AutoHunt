#nullable enable
using System;

namespace HuntTrainAuto.Tests.Windows;

public sealed class DependencyAvailabilityTests
{
	[Theory]
	[InlineData(true, "available")]
	[InlineData(false, "missing")]
	public void StatusLabel_matches_presence(bool available, string expected)
		=> Assert.Equal(expected, DependencyAvailability.StatusLabel(available));

	[Theory]
	[InlineData("vnavmesh", true, "vnavmesh: available")]
	[InlineData("Teleporter", false, "Teleporter: missing")]
	[InlineData("Rotation Solver Reborn", true, "Rotation Solver Reborn: available")]
	[InlineData("Lifestream", false, "Lifestream: missing")]
	public void FormatLine_includes_name_and_status(string name, bool available, string expected)
		=> Assert.Equal(expected, DependencyAvailability.FormatLine(name, available));

	[Fact]
	public void Display_names_match_integrations()
	{
		Assert.Equal("Teleporter", DependencyAvailability.TeleporterDisplayName);
		Assert.Equal("Lifestream", DependencyAvailability.LifestreamDisplayName);
		Assert.Equal("vnavmesh", DependencyAvailability.VnavmeshDisplayName);
		Assert.Equal("Rotation Solver Reborn", DependencyAvailability.RsrDisplayName);
		Assert.Equal("HuntAlerts", DependencyAvailability.HuntAlertsDisplayName);
	}

	[Fact]
	public void SafeIsAvailable_returns_probe_result()
	{
		Assert.True(DependencyAvailability.SafeIsAvailable(() => true));
		Assert.False(DependencyAvailability.SafeIsAvailable(() => false));
	}

	[Fact]
	public void SafeIsAvailable_swallows_probe_exceptions()
	{
		Assert.False(DependencyAvailability.SafeIsAvailable(
			() => throw new InvalidOperationException("ipc missing")));
	}
}
