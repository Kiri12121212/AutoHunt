#nullable enable
using System.Collections.Generic;

namespace HuntTrainAuto.Tests.Combat;

public sealed class RsrCommandsTests
{
	[Fact]
	public void Plugin_and_ipc_names_match_AD_RSR_contract()
	{
		Assert.Equal("RotationSolver", RsrCommands.PluginInternalName);
		Assert.Equal("RotationSolverReborn", RsrCommands.IpcPrefix);
	}

	[Theory]
	[InlineData(RsrTargetHostileType.AllTargetsCanAttack, "HostileType AllTargetsCanAttack")]
	[InlineData(RsrTargetHostileType.TargetsHaveTarget, "HostileType TargetsHaveTarget")]
	[InlineData(RsrTargetHostileType.AllTargetsWhenSoloInDuty, "HostileType AllTargetsWhenSoloInDuty")]
	[InlineData(RsrTargetHostileType.AllTargetsWhenSolo, "HostileType AllTargetsWhenSolo")]
	public void HostileTypeSetting_formats_enum_name(RsrTargetHostileType type, string expected)
		=> Assert.Equal(expected, RsrCommands.HostileTypeSetting(type));

	[Theory]
	[InlineData(true, "FriendlyPartyNpcHealRaise3 true")]
	[InlineData(false, "FriendlyPartyNpcHealRaise3 false")]
	public void FriendlyPartyNpcHealRaise3Setting(bool enabled, string expected)
		=> Assert.Equal(expected, RsrCommands.FriendlyPartyNpcHealRaise3Setting(enabled));

	[Theory]
	[InlineData(false, "AutoOffAfterCombat false")]
	[InlineData(true, "AutoOffAfterCombat true")]
	public void AutoOffAfterCombatSetting(bool enabled, string expected)
		=> Assert.Equal(expected, RsrCommands.AutoOffAfterCombatSetting(enabled));

	[Fact]
	public void DefaultRotationAutoSettings_matches_AD_sequence()
	{
		var settings = RsrCommands.DefaultRotationAutoSettings();
		Assert.Equal(
			[
				"HostileType AllTargetsCanAttack",
				"FriendlyPartyNpcHealRaise3 true",
				"AutoOffAfterCombat false",
			],
			settings);
	}

	[Fact]
	public void DefaultRotationAutoSettings_respects_hostile_override()
	{
		var settings = RsrCommands.DefaultRotationAutoSettings(RsrTargetHostileType.TargetsHaveTarget);
		Assert.Equal("HostileType TargetsHaveTarget", settings[0]);
	}

	[Theory]
	[InlineData(true, RsrTargetingType.HighHP)]
	[InlineData(false, RsrTargetingType.LowHP)]
	public void DefaultTargetingForTankRole(bool isTank, RsrTargetingType expected)
		=> Assert.Equal(expected, RsrCommands.DefaultTargetingForTankRole(isTank));

	[Fact]
	public void StateCommandType_underlying_values_match_RSR()
	{
		Assert.Equal(0, (byte)RsrStateCommandType.Off);
		Assert.Equal(1, (byte)RsrStateCommandType.Auto);
		Assert.Equal(2, (byte)RsrStateCommandType.TargetOnly);
		Assert.Equal(3, (byte)RsrStateCommandType.Manual);
		Assert.Equal(4, (byte)RsrStateCommandType.AutoDuty);
		Assert.Equal(5, (byte)RsrStateCommandType.Henched);
		Assert.Equal(6, (byte)RsrStateCommandType.PvP);
	}

	[Fact]
	public void OtherCommandType_Settings_is_zero()
		=> Assert.Equal(0, (byte)RsrOtherCommandType.Settings);

	[Fact]
	public void TargetingType_HighHP_LowHP_match_RSR()
	{
		Assert.Equal(2, (int)RsrTargetingType.HighHP);
		Assert.Equal(3, (int)RsrTargetingType.LowHP);
	}

	[Fact]
	public void TargetHostileType_AllTargetsCanAttack_is_zero()
		=> Assert.Equal(0, (byte)RsrTargetHostileType.AllTargetsCanAttack);

	[Fact]
	public void IsPluginLoaded_true_when_RotationSolver_loaded()
	{
		IEnumerable<(string InternalName, bool IsLoaded)> plugins =
		[
			("vnavmesh", true),
			("RotationSolver", true),
		];
		Assert.True(RsrCommands.IsPluginLoaded(plugins));
	}

	[Fact]
	public void IsPluginLoaded_false_when_missing_or_unloaded()
	{
		Assert.False(RsrCommands.IsPluginLoaded([("RotationSolver", false)]));
		Assert.False(RsrCommands.IsPluginLoaded([("OtherPlugin", true)]));
		Assert.False(RsrCommands.IsPluginLoaded([]));
	}
}
