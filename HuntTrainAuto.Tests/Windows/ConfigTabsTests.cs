#nullable enable
using System;

namespace HuntTrainAuto.Tests.Windows;

public sealed class ConfigTabsTests
{
	[Fact]
	public void Labels_match_phase8_tabs()
	{
		Assert.Equal(
			["Settings", "Mount", "Follow", "Combat", "Integrations"],
			ConfigTabs.Labels);
		Assert.Equal(5, ConfigTabs.Labels.Length);
		Assert.Equal(0, ConfigTabs.Settings);
		Assert.Equal(1, ConfigTabs.Mount);
		Assert.Equal(2, ConfigTabs.Follow);
		Assert.Equal(3, ConfigTabs.Combat);
		Assert.Equal(4, ConfigTabs.Integrations);
	}

	[Theory]
	[InlineData(-3, 0)]
	[InlineData(0, 0)]
	[InlineData(2, 2)]
	[InlineData(4, 4)]
	[InlineData(99, 4)]
	public void ClampSelected_stays_in_range(int input, int expected)
		=> Assert.Equal(expected, ConfigTabs.ClampSelected(input));

	[Theory]
	[InlineData(0, "Settings")]
	[InlineData(1, "Mount")]
	[InlineData(4, "Integrations")]
	[InlineData(-1, "Settings")]
	public void LabelAt_uses_clamped_index(int index, string expected)
		=> Assert.Equal(expected, ConfigTabs.LabelAt(index));

	[Theory]
	[InlineData(-1, "Never")]
	[InlineData(0, "Random")]
	[InlineData(15, "Mount #15")]
	[InlineData(122, "Mount #122")]
	public void FormatMountSelection_labels_ids(int mount, string expected)
		=> Assert.Equal(expected, ConfigTabs.FormatMountSelection(mount));

	[Theory]
	[InlineData(-5, -1)]
	[InlineData(-1, -1)]
	[InlineData(0, 0)]
	[InlineData(42, 42)]
	public void ClampMountId_floors_at_never(int input, int expected)
		=> Assert.Equal(expected, ConfigTabs.ClampMountId(input));

	[Fact]
	public void FormatHuntAlertsPlaceholder_is_phase10_disabled()
		=> Assert.Equal(
			"HuntAlerts: Phase 10 — not wired",
			ConfigTabs.FormatHuntAlertsPlaceholder());

	[Theory]
	[InlineData(-10, 0)]
	[InlineData(0, 0)]
	[InlineData(700, 700)]
	[InlineData(50_000, 30_000)]
	public void ClampTeleportDelayMs_bounds(int input, int expected)
		=> Assert.Equal(expected, ConfigTabs.ClampTeleportDelayMs(input));

	[Theory]
	[InlineData(200, 700, 200, 700)]
	[InlineData(900, 100, 100, 900)]
	[InlineData(-5, 40_000, 0, 30_000)]
	[InlineData(500, 500, 500, 500)]
	public void ClampTeleportDelayRange_orders_and_clamps(
		int minIn,
		int maxIn,
		int minOut,
		int maxOut)
	{
		var (min, max) = ConfigTabs.ClampTeleportDelayRange(minIn, maxIn);
		Assert.Equal(minOut, min);
		Assert.Equal(maxOut, max);
	}

	[Theory]
	[InlineData(float.NaN, 3f)]
	[InlineData(float.PositiveInfinity, 3f)]
	[InlineData(-1f, 0f)]
	[InlineData(3f, 3f)]
	[InlineData(99f, 50f)]
	public void ClampAutoTeleportSkipDistance_bounds(float input, float expected)
		=> Assert.Equal(expected, ConfigTabs.ClampAutoTeleportSkipDistance(input));

	[Theory]
	[InlineData(float.NaN, 5f)]
	[InlineData(0.1f, 1f)]
	[InlineData(5f, 5f)]
	[InlineData(40f, 25f)]
	public void ClampFlagArrivalTolerance_bounds(float input, float expected)
		=> Assert.Equal(expected, ConfigTabs.ClampFlagArrivalTolerance(input));
}
