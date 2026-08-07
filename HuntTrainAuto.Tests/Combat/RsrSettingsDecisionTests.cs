#nullable enable

namespace HuntTrainAuto.Tests.Combat;

public sealed class RsrSettingsDecisionTests
{
	[Theory]
	[InlineData((byte)1, true)]
	[InlineData((byte)0, false)]
	[InlineData((byte)2, false)]
	[InlineData((byte)3, false)]
	[InlineData((byte)4, false)]
	public void IsTankRole_matches_ClassJob_Role_byte(byte role, bool expected)
		=> Assert.Equal(expected, RsrSettingsDecision.IsTankRole(role));

	[Fact]
	public void Defaults_match_AD()
	{
		Assert.Equal(RsrTargetHostileType.AllTargetsCanAttack, RsrSettingsDecision.DefaultHostileType);
		Assert.Equal(RsrTargetingType.HighHP, RsrSettingsDecision.DefaultTankTargeting);
		Assert.Equal(RsrTargetingType.LowHP, RsrSettingsDecision.DefaultNonTankTargeting);
	}

	[Theory]
	[InlineData(RsrTargetHostileType.AllTargetsCanAttack)]
	[InlineData(RsrTargetHostileType.TargetsHaveTarget)]
	[InlineData(RsrTargetHostileType.AllTargetsWhenSoloInDuty)]
	[InlineData(RsrTargetHostileType.AllTargetsWhenSolo)]
	[InlineData(RsrTargetHostileType.SoloDeepDungeonSmart)]
	public void ClampHostileType_keeps_defined(RsrTargetHostileType type)
		=> Assert.Equal(type, RsrSettingsDecision.ClampHostileType(type));

	[Fact]
	public void ClampHostileType_undefined_falls_back_to_AllTargetsCanAttack()
		=> Assert.Equal(
			RsrTargetHostileType.AllTargetsCanAttack,
			RsrSettingsDecision.ClampHostileType((RsrTargetHostileType)200));

	[Theory]
	[InlineData(RsrTargetingType.HighHP)]
	[InlineData(RsrTargetingType.LowHP)]
	[InlineData(RsrTargetingType.Nearest)]
	[InlineData(RsrTargetingType.Big)]
	public void ClampTargetingType_keeps_defined(RsrTargetingType type)
		=> Assert.Equal(type, RsrSettingsDecision.ClampTargetingType(type, RsrTargetingType.LowHP));

	[Fact]
	public void ClampTargetingType_undefined_uses_fallback()
		=> Assert.Equal(
			RsrTargetingType.HighHP,
			RsrSettingsDecision.ClampTargetingType((RsrTargetingType)999, RsrTargetingType.HighHP));

	[Theory]
	[InlineData(true, RsrTargetingType.HighHP)]
	[InlineData(false, RsrTargetingType.LowHP)]
	public void ResolveTargeting_role_defaults(bool isTank, RsrTargetingType expected)
		=> Assert.Equal(
			expected,
			RsrSettingsDecision.ResolveTargeting(
				isTank,
				RsrSettingsDecision.DefaultTankTargeting,
				RsrSettingsDecision.DefaultNonTankTargeting));

	[Fact]
	public void ResolveTargeting_uses_user_config_overrides()
	{
		Assert.Equal(
			RsrTargetingType.Nearest,
			RsrSettingsDecision.ResolveTargeting(true, RsrTargetingType.Nearest, RsrTargetingType.Farthest));
		Assert.Equal(
			RsrTargetingType.Farthest,
			RsrSettingsDecision.ResolveTargeting(false, RsrTargetingType.Nearest, RsrTargetingType.Farthest));
	}

	[Fact]
	public void ResolveTargeting_clamps_undefined_config_per_role_fallback()
	{
		Assert.Equal(
			RsrTargetingType.HighHP,
			RsrSettingsDecision.ResolveTargeting(true, (RsrTargetingType)999, RsrTargetingType.LowHP));
		Assert.Equal(
			RsrTargetingType.LowHP,
			RsrSettingsDecision.ResolveTargeting(false, RsrTargetingType.HighHP, (RsrTargetingType)999));
	}

	[Fact]
	public void Resolve_returns_clamped_hostile_and_role_targeting()
	{
		var (targeting, hostile) = RsrSettingsDecision.Resolve(
			isTank: true,
			hostileType: (RsrTargetHostileType)200,
			tankTargeting: RsrTargetingType.HighMaxHP,
			nonTankTargeting: RsrTargetingType.LowHP);

		Assert.Equal(RsrTargetHostileType.AllTargetsCanAttack, hostile);
		Assert.Equal(RsrTargetingType.HighMaxHP, targeting);
	}

	[Fact]
	public void HostileTypeSetting_uses_clamped_config_value()
	{
		var hostile = RsrSettingsDecision.ClampHostileType(RsrTargetHostileType.TargetsHaveTarget);
		Assert.Equal(
			"HostileType TargetsHaveTarget",
			RsrCommands.HostileTypeSetting(hostile));
	}

	[Fact]
	public void DefaultRotationAutoSettings_keeps_AutoOffAfterCombat_false()
	{
		var settings = RsrCommands.DefaultRotationAutoSettings(RsrSettingsDecision.DefaultHostileType);
		Assert.Contains("AutoOffAfterCombat false", settings);
		Assert.Equal("HostileType AllTargetsCanAttack", settings[0]);
	}
}
