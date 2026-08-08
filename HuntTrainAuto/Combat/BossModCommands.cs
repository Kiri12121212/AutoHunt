#nullable enable
using System;
using System.Collections.Generic;

namespace HuntTrainAuto.Combat;

/// <summary>
/// Pure BossMod / BossModReborn helpers (no CallGate). Unit-tested without Dalamud.
/// Both plugins register CallGates under prefix <c>BossMod</c>; detect via InternalName.
/// </summary>
public static class BossModCommands
{
	/// <summary>awgil Boss Mod Dalamud InternalName.</summary>
	public const string VbmInternalName = "BossMod";

	/// <summary>BossModReborn Dalamud InternalName.</summary>
	public const string BmrInternalName = "BossModReborn";

	/// <summary>Shared CallGate prefix used by both VBM and BMR.</summary>
	public const string IpcPrefix = "BossMod";

	public const string ConfigurationChannel = IpcPrefix + ".Configuration";

	/// <summary>BMR-only: pause AI movement without tearing down behaviour.</summary>
	public const string PauseMovementChannel = IpcPrefix + ".AI.PauseMovement";

	public static IReadOnlyList<string> EnableAiConfigArgs { get; } =
		["AIConfig", "Enabled", "true"];

	public static IReadOnlyList<string> DisableAiConfigArgs { get; } =
		["AIConfig", "Enabled", "false"];

	/// <summary>Configuration get (no value) — returns current Enabled when supported.</summary>
	public static IReadOnlyList<string> GetAiEnabledConfigArgs { get; } =
		["AIConfig", "Enabled"];

	/// <summary>
	/// With RSR owning GCD: BM should not auto-target / cast.
	/// Applies to VBM AIConfig and BMR Automovement AIConfig.
	/// </summary>
	public static IReadOnlyList<string> ForbidActionsTrueArgs { get; } =
		["AIConfig", "ForbidActions", "true"];

	public static IReadOnlyList<string> ForbidMovementFalseArgs { get; } =
		["AIConfig", "ForbidMovement", "false"];

	/// <summary>BMR slash: start Automovement behaviour (<c>SwitchToFollow</c>).</summary>
	public const string BmrEnableChatCommand = "/bmrai on";

	/// <summary>BMR slash: stop Automovement behaviour (<c>SwitchToIdle</c>).</summary>
	public const string BmrDisableChatCommand = "/bmrai off";

	/// <summary>VBM slash fallback when Configuration IPC soft-fails.</summary>
	public const string VbmEnableChatCommand = "/vbm cfg AIConfig Enabled true";

	public const string VbmDisableChatCommand = "/vbm cfg AIConfig Enabled false";

	public static string DisplayName(BossModProviderKind kind)
		=> kind switch
		{
			BossModProviderKind.Vbm => "Boss Mod",
			BossModProviderKind.Bmr => "BossMod Reborn",
			_ => "BossMod",
		};

	public static string EnableChatCommand(BossModProviderKind kind)
		=> kind switch
		{
			BossModProviderKind.Bmr => BmrEnableChatCommand,
			BossModProviderKind.Vbm => VbmEnableChatCommand,
			_ => string.Empty,
		};

	public static string DisableChatCommand(BossModProviderKind kind)
		=> kind switch
		{
			BossModProviderKind.Bmr => BmrDisableChatCommand,
			BossModProviderKind.Vbm => VbmDisableChatCommand,
			_ => string.Empty,
		};

	/// <summary>
	/// Pick a single provider. Both plugins collide on <c>BossMod.*</c> CallGates —
	/// only one should be loaded; preference breaks the tie if both appear loaded.
	/// </summary>
	public static BossModProviderKind ResolveProvider(
		bool vbmLoaded,
		bool bmrLoaded,
		BossModPreference preference = BossModPreference.PreferBmr)
	{
		if (!vbmLoaded && !bmrLoaded)
			return BossModProviderKind.None;
		if (vbmLoaded && !bmrLoaded)
			return BossModProviderKind.Vbm;
		if (!vbmLoaded && bmrLoaded)
			return BossModProviderKind.Bmr;
		return preference == BossModPreference.PreferVbm
			? BossModProviderKind.Vbm
			: BossModProviderKind.Bmr;
	}

	public static bool IsPluginLoaded(
		IEnumerable<(string InternalName, bool IsLoaded)> plugins,
		string internalName)
	{
		foreach (var plugin in plugins)
		{
			if (plugin.IsLoaded
			    && string.Equals(plugin.InternalName, internalName, StringComparison.Ordinal))
				return true;
		}

		return false;
	}

	public static BossModProviderKind ResolveFromPlugins(
		IEnumerable<(string InternalName, bool IsLoaded)> plugins,
		BossModPreference preference = BossModPreference.PreferBmr)
	{
		var vbm = IsPluginLoaded(plugins, VbmInternalName);
		var bmr = IsPluginLoaded(plugins, BmrInternalName);
		return ResolveProvider(vbm, bmr, preference);
	}

	public static BossModPreference ClampPreference(BossModPreference preference)
		=> Enum.IsDefined(preference) ? preference : BossModPreference.PreferBmr;
}
