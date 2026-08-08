#nullable enable
using System.Collections.Generic;

namespace HuntTrainAuto.Tests.Combat;

public sealed class BossModCommandsTests
{
	[Fact]
	public void ResolveProvider_none_when_neither_loaded()
		=> Assert.Equal(
			BossModProviderKind.None,
			BossModCommands.ResolveProvider(false, false));

	[Fact]
	public void ResolveProvider_single_loaded_wins()
	{
		Assert.Equal(
			BossModProviderKind.Vbm,
			BossModCommands.ResolveProvider(vbmLoaded: true, bmrLoaded: false));
		Assert.Equal(
			BossModProviderKind.Bmr,
			BossModCommands.ResolveProvider(vbmLoaded: false, bmrLoaded: true));
	}

	[Theory]
	[InlineData(BossModPreference.PreferBmr, BossModProviderKind.Bmr)]
	[InlineData(BossModPreference.PreferVbm, BossModProviderKind.Vbm)]
	public void ResolveProvider_preference_when_both_loaded(
		BossModPreference preference,
		BossModProviderKind expected)
		=> Assert.Equal(
			expected,
			BossModCommands.ResolveProvider(true, true, preference));

	[Fact]
	public void ResolveFromPlugins_reads_internal_names()
	{
		IEnumerable<(string, bool)> plugins =
		[
			("BossModReborn", true),
			("BossMod", false),
			("RotationSolver", true),
		];
		Assert.Equal(
			BossModProviderKind.Bmr,
			BossModCommands.ResolveFromPlugins(plugins));
	}

	[Fact]
	public void Chat_commands_match_provider()
	{
		Assert.Equal("/bmrai on", BossModCommands.EnableChatCommand(BossModProviderKind.Bmr));
		Assert.Equal("/bmrai off", BossModCommands.DisableChatCommand(BossModProviderKind.Bmr));
		Assert.Contains("AIConfig", BossModCommands.EnableChatCommand(BossModProviderKind.Vbm));
		Assert.Contains("Enabled", BossModCommands.DisableChatCommand(BossModProviderKind.Vbm));
		Assert.Equal(string.Empty, BossModCommands.EnableChatCommand(BossModProviderKind.None));
	}

	[Fact]
	public void Config_args_enable_disable_and_coexistence()
	{
		Assert.Equal(["AIConfig", "Enabled", "true"], BossModCommands.EnableAiConfigArgs);
		Assert.Equal(["AIConfig", "Enabled", "false"], BossModCommands.DisableAiConfigArgs);
		Assert.Equal(["AIConfig", "ForbidActions", "true"], BossModCommands.ForbidActionsTrueArgs);
		Assert.Equal(["AIConfig", "ForbidMovement", "false"], BossModCommands.ForbidMovementFalseArgs);
	}

	[Fact]
	public void ClampPreference_defaults_undefined()
		=> Assert.Equal(
			BossModPreference.PreferBmr,
			BossModCommands.ClampPreference((BossModPreference)99));

	[Fact]
	public void DisplayName_labels_providers()
	{
		Assert.Equal("Boss Mod", BossModCommands.DisplayName(BossModProviderKind.Vbm));
		Assert.Equal("BossMod Reborn", BossModCommands.DisplayName(BossModProviderKind.Bmr));
		Assert.Equal("BossMod", BossModCommands.DisplayName(BossModProviderKind.None));
	}
}
