#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using HuntTrainAuto.Combat;
using HuntTrainAuto.Contracts;
using HuntTrainAuto.Logging;

namespace HuntTrainAuto.Services;

/// <summary>
/// Soft-fail wrapper for BossMod / BossModReborn.
/// Both register CallGates as <c>BossMod.*</c>; presence is via Dalamud InternalName.
/// VBM AI: <c>BossMod.Configuration</c> (<c>IReadOnlyList&lt;string&gt;</c>) → AIConfig.Enabled.
/// BMR AI: chat <c>/bmrai on|off</c> (no Enabled field; SwitchToFollow/Idle).
/// Coexistence: ForbidActions=true so RSR keeps GCD; ForbidMovement=false for dodge.
/// </summary>
public sealed class BossModIpc : IBossModService
{
	private readonly IDalamudPluginInterface pluginInterface;
	private readonly IChatOutput chat;
	private readonly Func<BossModPreference> resolvePreference;
	private readonly IPluginLog? log;
	private readonly Func<bool>? debugEnabled;

	/// <summary>VBM signature: <c>Func&lt;IReadOnlyList&lt;string&gt;, bool, object&gt;</c>.</summary>
	private readonly ICallGateSubscriber<IReadOnlyList<string>, bool, object> configurationVbm;

	/// <summary>BMR signature: <c>Func&lt;List&lt;string&gt;, bool, object&gt;</c>.</summary>
	private readonly ICallGateSubscriber<List<string>, bool, object> configurationBmr;

	/// <summary>BMR-only: <c>Action&lt;bool&gt;</c> PauseMovement.</summary>
	private readonly ICallGateSubscriber<bool, object> pauseMovement;

	public BossModIpc(
		IDalamudPluginInterface pluginInterface,
		IChatOutput chat,
		Func<BossModPreference> resolvePreference,
		IPluginLog? log = null,
		Func<bool>? debugEnabled = null)
	{
		this.pluginInterface = pluginInterface;
		this.chat = chat;
		this.resolvePreference = resolvePreference;
		this.log = log;
		this.debugEnabled = debugEnabled;
		configurationVbm = pluginInterface.GetIpcSubscriber<IReadOnlyList<string>, bool, object>(
			BossModCommands.ConfigurationChannel);
		configurationBmr = pluginInterface.GetIpcSubscriber<List<string>, bool, object>(
			BossModCommands.ConfigurationChannel);
		pauseMovement = pluginInterface.GetIpcSubscriber<bool, object>(
			BossModCommands.PauseMovementChannel);
	}

	/// <inheritdoc />
	public BossModProviderKind ActiveProvider
	{
		get
		{
			try
			{
				return BossModCommands.ResolveFromPlugins(
					pluginInterface.InstalledPlugins.Select(p => (p.InternalName, p.IsLoaded)),
					BossModCommands.ClampPreference(resolvePreference()));
			}
			catch (Exception ex)
			{
				DebugSoftFail("availability probe", ex);
				return BossModProviderKind.None;
			}
		}
	}

	/// <inheritdoc />
	public bool IsAvailable => ActiveProvider != BossModProviderKind.None;

	/// <inheritdoc />
	public bool EnableAi(bool coexistWithRsr = true)
	{
		var provider = ActiveProvider;
		if (provider == BossModProviderKind.None)
		{
			Debug("EnableAi skipped: provider unavailable");
			return false;
		}

		if (coexistWithRsr)
			ApplyCoexistenceSettings(provider);

		var enabled = provider switch
		{
			BossModProviderKind.Vbm => EnableVbm(),
			BossModProviderKind.Bmr => EnableBmr(),
			_ => false,
		};
		Debug(enabled ? $"EnableAi succeeded: provider={provider}" : $"EnableAi failed: provider={provider}");
		return enabled;
	}

	/// <inheritdoc />
	public bool DisableAi()
	{
		var provider = ActiveProvider;
		if (provider == BossModProviderKind.None)
		{
			Debug("DisableAi skipped: provider unavailable");
			return false;
		}

		var disabled = provider switch
		{
			BossModProviderKind.Vbm => DisableVbm(),
			BossModProviderKind.Bmr => DisableBmr(),
			_ => false,
		};
		Debug(disabled ? $"DisableAi succeeded: provider={provider}" : $"DisableAi failed: provider={provider}");
		return disabled;
	}

	private bool EnableVbm()
	{
		if (TryConfigurationVbm(BossModCommands.EnableAiConfigArgs))
			return true;
		return chat.TryExecuteCommand(BossModCommands.VbmEnableChatCommand);
	}

	private bool DisableVbm()
	{
		if (TryConfigurationVbm(BossModCommands.DisableAiConfigArgs))
			return true;
		return chat.TryExecuteCommand(BossModCommands.VbmDisableChatCommand);
	}

	private bool EnableBmr()
	{
		// Prefer Configuration when present (newer BMR); always also /bmrai on for Beh.
		_ = TryConfigurationBmr(BossModCommands.EnableAiConfigArgs);
		var chatOk = chat.TryExecuteCommand(BossModCommands.BmrEnableChatCommand);
		// When Enabled is readable, require it true (chat alone can soft-succeed with no effect).
		var enabled = TryGetAiEnabled();
		if (enabled is bool e)
			return e;
		return chatOk;
	}

	private bool DisableBmr()
	{
		_ = TryConfigurationBmr(BossModCommands.DisableAiConfigArgs);
		var chatOk = chat.TryExecuteCommand(BossModCommands.BmrDisableChatCommand);
		var enabled = TryGetAiEnabled();
		if (enabled is bool e)
			return !e;
		return chatOk;
	}

	/// <inheritdoc />
	public bool? TryGetAiEnabled()
	{
		try
		{
			var provider = ActiveProvider;
			object? raw = provider switch
			{
				BossModProviderKind.Vbm => configurationVbm.InvokeFunc(
					BossModCommands.GetAiEnabledConfigArgs,
					false),
				BossModProviderKind.Bmr => configurationBmr.InvokeFunc(
					[.. BossModCommands.GetAiEnabledConfigArgs],
					false),
				_ => null,
			};
			return ParseBoolConfig(raw);
		}
		catch (Exception ex)
		{
			DebugSoftFail("GetAiEnabled", ex);
			return null;
		}
	}

	private static bool? ParseBoolConfig(object? raw)
	{
		switch (raw)
		{
			case bool b:
				return b;
			case string s when bool.TryParse(s, out var parsed):
				return parsed;
			default:
				return null;
		}
	}

	private void ApplyCoexistenceSettings(BossModProviderKind provider)
	{
		// RSR owns GCD; BM owns dodge movement only.
		_ = TryConfiguration(provider, BossModCommands.ForbidActionsTrueArgs);
		_ = TryConfiguration(provider, BossModCommands.ForbidMovementFalseArgs);
		if (provider == BossModProviderKind.Bmr)
			TryPauseMovement(false);
	}

	private bool TryConfiguration(BossModProviderKind provider, IReadOnlyList<string> args)
		=> provider switch
		{
			BossModProviderKind.Vbm => TryConfigurationVbm(args),
			BossModProviderKind.Bmr => TryConfigurationBmr(args),
			_ => false,
		};

	private bool TryConfigurationVbm(IReadOnlyList<string> args)
	{
		try
		{
			configurationVbm.InvokeFunc(args, true);
			return true;
		}
		catch (Exception ex)
		{
			DebugSoftFail("Configuration(VBM)", ex);
			return false;
		}
	}

	private bool TryConfigurationBmr(IReadOnlyList<string> args)
	{
		try
		{
			configurationBmr.InvokeFunc([.. args], true);
			return true;
		}
		catch (Exception ex)
		{
			DebugSoftFail("Configuration(BMR)", ex);
			return false;
		}
	}

	private void TryPauseMovement(bool pause)
	{
		try
		{
			pauseMovement.InvokeAction(pause);
		}
		catch (Exception ex)
		{
			DebugSoftFail("PauseMovement", ex);
		}
	}

	public void Dispose()
	{
		// Subscriber only — no event subscriptions to tear down.
	}

	private bool IsDebugEnabled()
		=> debugEnabled?.Invoke() ?? false;

	private void Debug(string message)
	{
		if (log != null)
			DebugBehavior.Debug(log, IsDebugEnabled(), "BossMod", message);
	}

	private void DebugSoftFail(string operation, Exception ex)
	{
		if (log != null)
			DebugBehavior.DebugThrottled(
				log, IsDebugEnabled(), $"bossmod.{operation}", 2_000, Environment.TickCount64, "BossMod",
				$"{operation} unavailable/soft-fail: {ex.Message}");
	}
}
