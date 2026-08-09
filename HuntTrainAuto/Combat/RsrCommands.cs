#nullable enable
using System.Collections.Generic;

namespace HuntTrainAuto.Combat;

/// <summary>
/// Pure RSR settings / mode helpers (no CallGate). Unit-tested without Dalamud.
/// </summary>
public static class RsrCommands
{
	/// <summary>Dalamud InternalName used by AD <c>IsReady("RotationSolver")</c>.</summary>
	public const string PluginInternalName = "RotationSolver";

	/// <summary>EzIPC prefix registered by RSR <c>IPCProvider</c>.</summary>
	public const string IpcPrefix = "RotationSolverReborn";

	public static string HostileTypeSetting(RsrTargetHostileType hostileType)
		=> $"HostileType {hostileType}";

	public static string FriendlyPartyNpcHealRaise3Setting(bool enabled = true)
		=> $"FriendlyPartyNpcHealRaise3 {(enabled ? "true" : "false")}";

	public static string AutoOffAfterCombatSetting(bool enabled = false)
		=> $"AutoOffAfterCombat {(enabled ? "true" : "false")}";

	/// <summary>
	/// AD <c>RotationAuto</c> settings sequence before
	/// <c>AutodutyChangeOperatingMode</c>.
	/// </summary>
	public static IReadOnlyList<string> DefaultRotationAutoSettings(
		RsrTargetHostileType hostileType = RsrTargetHostileType.TargetsHaveTarget)
		=>
		[
			HostileTypeSetting(RsrSettingsDecision.ClampHostileType(hostileType)),
			FriendlyPartyNpcHealRaise3Setting(true),
			AutoOffAfterCombatSetting(false),
		];

	/// <summary>
	/// Hunt tank vs non-tank targeting defaults (HighMaxHP).
	/// Prefer <see cref="RsrSettingsDecision.ResolveTargeting"/> when config is available.
	/// </summary>
	public static RsrTargetingType DefaultTargetingForTankRole(bool isTank)
		=> RsrSettingsDecision.ResolveTargeting(
			isTank,
			RsrSettingsDecision.DefaultTankTargeting,
			RsrSettingsDecision.DefaultNonTankTargeting);

	/// <summary>
	/// AD <c>IPCSubscriber_Common.IsReady</c> equivalent over Dalamud
	/// <c>InstalledPlugins</c> snapshots (pure).
	/// </summary>
	public static bool IsPluginLoaded(
		IEnumerable<(string InternalName, bool IsLoaded)> plugins,
		string internalName = PluginInternalName)
	{
		foreach (var plugin in plugins)
		{
			if (plugin.IsLoaded
			    && string.Equals(plugin.InternalName, internalName, System.StringComparison.Ordinal))
				return true;
		}

		return false;
	}
}
