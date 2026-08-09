#nullable enable
using System.Numerics;

namespace HuntTrainAuto.Tests.Combat;

public sealed class FakeHuntDecisionTests
{
	[Fact]
	public void OffsetWorldXZ_preserves_y_and_moves_xz()
	{
		var origin = new Vector3(10f, 5f, 20f);
		var p = FakeHuntDecision.OffsetWorldXZ(origin, 50f, 0f);
		Assert.Equal(60f, p.X, 3);
		Assert.Equal(5f, p.Y, 3);
		Assert.Equal(20f, p.Z, 3);
	}

	[Fact]
	public void RawFromWorldXZ_round_trips_WorldXZFromRaw()
	{
		var (rawX, rawY) = FakeHuntDecision.RawFromWorldXZ(12.345f, -9.001f);
		var xz = FlagWorldPosition.WorldXZFromRaw(rawX, rawY);
		Assert.Equal(12.345f, xz.X, 3);
		Assert.Equal(-9.001f, xz.Y, 3);
	}

	[Theory]
	[InlineData(FakeHuntPreset.Near, FakeHuntDecision.NearFlagDistanceYalms)]
	[InlineData(FakeHuntPreset.Far, FakeHuntDecision.FarFlagDistanceYalms)]
	[InlineData(FakeHuntPreset.MapFlag, FakeHuntDecision.NearFlagDistanceYalms)]
	[InlineData(FakeHuntPreset.InstanceSwap, FakeHuntDecision.FarFlagDistanceYalms)]
	public void FlagDistanceForPreset(FakeHuntPreset preset, float expected)
		=> Assert.Equal(expected, FakeHuntDecision.FlagDistanceForPreset(preset));

	[Fact]
	public void Near_is_former_far_beyond_already_close()
		=> Assert.Equal(250f, FakeHuntDecision.NearFlagDistanceYalms);

	[Fact]
	public void Far_covers_most_of_typical_map()
	{
		Assert.True(FakeHuntDecision.FarFlagDistanceYalms >= 1000f);
		Assert.True(FakeHuntDecision.FarFlagDistanceYalms > FakeHuntDecision.NearFlagDistanceYalms);
	}

	[Fact]
	public void Describe_formats_preset_and_distance()
		=> Assert.Equal(
			"preset=Far, flagDistance=1000.0",
			FakeHuntDecision.Describe(FakeHuntPreset.Far));

	[Theory]
	[InlineData(0, 2)]
	[InlineData(1, 2)]
	[InlineData(2, 1)]
	[InlineData(3, 1)]
	public void AlternateReportedInstance_flips_1_and_2(int current, int expected)
		=> Assert.Equal(expected, FakeHuntDecision.AlternateReportedInstance(current));

	[Theory]
	[InlineData(0f, FakeHuntDecision.FakeARankMinDistanceYalms)]
	[InlineData(1f, FakeHuntDecision.FakeARankMaxDistanceYalms)]
	public void FakeARankDistanceYalms_clamps_unit(float unit, float expected)
		=> Assert.Equal(expected, FakeHuntDecision.FakeARankDistanceYalms(unit));

	[Fact]
	public void ShouldAutoEndCombat_after_deadline()
	{
		Assert.False(FakeHuntDecision.ShouldAutoEndCombat(1000, 2000, 5000));
		Assert.True(FakeHuntDecision.ShouldAutoEndCombat(1000, 6000, 5000));
		Assert.False(FakeHuntDecision.ShouldAutoEndCombat(0, 99999, 5000));
	}

	[Theory]
	[InlineData(true, false, true)]
	[InlineData(true, true, false)]
	[InlineData(false, false, false)]
	[InlineData(false, true, false)]
	public void ShouldEnableCombatAi_SkipsFakeHunt(
		bool inCombatPhase,
		bool fakeHuntActive,
		bool expected)
		=> Assert.Equal(
			expected,
			FakeHuntDecision.ShouldEnableCombatAi(inCombatPhase, fakeHuntActive));

	[Fact]
	public void PlaceNameForPreset_is_stable()
	{
		Assert.Equal("FakeHunt Near", FakeHuntDecision.PlaceNameForPreset(FakeHuntPreset.Near));
		Assert.Equal("FakeHunt Far", FakeHuntDecision.PlaceNameForPreset(FakeHuntPreset.Far));
		Assert.Equal("FakeHunt MapFlag", FakeHuntDecision.PlaceNameForPreset(FakeHuntPreset.MapFlag));
		Assert.Equal(
			"FakeHunt InstanceSwap",
			FakeHuntDecision.PlaceNameForPreset(FakeHuntPreset.InstanceSwap));
	}
}
