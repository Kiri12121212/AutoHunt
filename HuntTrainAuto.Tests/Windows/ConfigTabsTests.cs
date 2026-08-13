#nullable enable
using System;

namespace HuntTrainAuto.Tests.Windows;

public sealed class ConfigTabsTests
{
	[Fact]
	public void Labels_match()
	{
		Assert.Equal(
			["Status", "Hunt", "Combat", "Plugins", "Debug"],
			ConfigTabs.Labels);
		Assert.Equal(5, ConfigTabs.Labels.Length);
		Assert.Equal(0, ConfigTabs.Status);
		Assert.Equal(1, ConfigTabs.Hunt);
		Assert.Equal(2, ConfigTabs.Combat);
		Assert.Equal(3, ConfigTabs.Plugins);
		Assert.Equal(4, ConfigTabs.Debug);
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
	[InlineData(-3, 0)]
	[InlineData(0, 0)]
	[InlineData(2, 2)]
	[InlineData(3, 3)]
	[InlineData(4, 3)]
	[InlineData(6, 3)]
	[InlineData(99, 3)]
	public void ClampSelected_stays_in_range_prod(int input, int expected)
		=> Assert.Equal(expected, ConfigTabs.ClampSelected(input, isDev: false));

	[Theory]
	[InlineData(true, 5)]
	[InlineData(false, 4)]
	public void VisibleTabCount_by_build(bool isDev, int expected)
		=> Assert.Equal(expected, ConfigTabs.VisibleTabCount(isDev));

	[Theory]
	[InlineData(true, true)]
	[InlineData(false, false)]
	public void IsDebugVisible_by_build(bool isDev, bool expected)
		=> Assert.Equal(expected, ConfigTabs.IsDebugVisible(isDev));

	[Theory]
	[InlineData(0, "Status")]
	[InlineData(1, "Hunt")]
	[InlineData(4, "Debug")]
	[InlineData(-1, "Status")]
	public void LabelAt_uses_clamped_index(int index, string expected)
		=> Assert.Equal(expected, ConfigTabs.LabelAt(index));

	[Theory]
	[InlineData(4, "Plugins")]
	[InlineData(6, "Plugins")]
	public void LabelAt_prod_clamps_to_last_visible(int index, string expected)
		=> Assert.Equal(expected, ConfigTabs.LabelAt(index, isDev: false));

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
	[InlineData(float.NaN, 150f)]
	[InlineData(float.PositiveInfinity, 150f)]
	[InlineData(-1f, 0f)]
	[InlineData(150f, 150f)]
	[InlineData(99f, 99f)]
	[InlineData(999f, 500f)]
	public void ClampAutoTeleportSkipDistance_bounds(float input, float expected)
		=> Assert.Equal(expected, ConfigTabs.ClampAutoTeleportSkipDistance(input));

	[Theory]
	[InlineData(3f, 150f)]
	[InlineData(0f, 0f)]
	[InlineData(50f, 500f)]
	[InlineData(10f, 500f)]
	public void ScaleLegacyMapSkipDistanceToYalms_maps_old_units(float legacy, float expected)
		=> Assert.Equal(expected, ConfigTabs.ScaleLegacyMapSkipDistanceToYalms(legacy));

	[Theory]
	[InlineData(0, true)]
	[InlineData(1, true)]
	[InlineData(2, false)]
	public void NeedsYalmSkipDistanceMigration_by_version(int version, bool expected)
		=> Assert.Equal(expected, ConfigTabs.NeedsYalmSkipDistanceMigration(version));

	[Theory]
	[InlineData(0, true)]
	[InlineData(2, true)]
	[InlineData(3, false)]
	public void NeedsHuntRsrTargetingMigration_by_version(int version, bool expected)
		=> Assert.Equal(expected, ConfigTabs.NeedsHuntRsrTargetingMigration(version));

	[Fact]
	public void TryMigrateHuntRsrTargeting_upgrades_ad_defaults()
	{
		var hostile = RsrTargetHostileType.AllTargetsCanAttack;
		var tank = RsrTargetingType.HighHP;
		var nonTank = RsrTargetingType.LowHP;
		Assert.True(ConfigTabs.TryMigrateHuntRsrTargeting(ref hostile, ref tank, ref nonTank));
		Assert.Equal(RsrTargetHostileType.TargetsHaveTarget, hostile);
		Assert.Equal(RsrTargetingType.HighMaxHP, tank);
		Assert.Equal(RsrTargetingType.HighMaxHP, nonTank);
	}

	[Fact]
	public void TryMigrateHuntRsrTargeting_keeps_custom_choices()
	{
		var hostile = RsrTargetHostileType.AllTargetsWhenSolo;
		var tank = RsrTargetingType.Nearest;
		var nonTank = RsrTargetingType.Farthest;
		Assert.False(ConfigTabs.TryMigrateHuntRsrTargeting(ref hostile, ref tank, ref nonTank));
		Assert.Equal(RsrTargetHostileType.AllTargetsWhenSolo, hostile);
		Assert.Equal(RsrTargetingType.Nearest, tank);
		Assert.Equal(RsrTargetingType.Farthest, nonTank);
	}

	[Theory]
	[InlineData(float.NaN, 5f)]
	[InlineData(0.1f, 3f)]
	[InlineData(5f, 5f)]
	[InlineData(40f, 25f)]
	public void ClampFlagArrivalTolerance_bounds(float input, float expected)
		=> Assert.Equal(expected, ConfigTabs.ClampFlagArrivalTolerance(input));

	[Theory]
	[InlineData(float.NaN, 5f)]
	[InlineData(-1f, 0f)]
	[InlineData(5f, 5f)]
	[InlineData(40f, 30f)]
	public void ClampTeleportCastSeconds_bounds(float input, float expected)
		=> Assert.Equal(expected, ConfigTabs.ClampTeleportCastSeconds(input));

	[Theory]
	[InlineData(float.NaN, 8f)]
	[InlineData(-1f, 0f)]
	[InlineData(8f, 8f)]
	[InlineData(99f, 60f)]
	public void ClampTeleportLoadEstimateSeconds_bounds(float input, float expected)
		=> Assert.Equal(expected, ConfigTabs.ClampTeleportLoadEstimateSeconds(input));

	[Theory]
	[InlineData(float.NaN, 20f)]
	[InlineData(0f, 1f)]
	[InlineData(20f, 20f)]
	[InlineData(100f, 80f)]
	public void ClampMountSpeedYalmsPerSec_bounds(float input, float expected)
		=> Assert.Equal(expected, ConfigTabs.ClampMountSpeedYalmsPerSec(input));
}
