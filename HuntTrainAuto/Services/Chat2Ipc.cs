#nullable enable
using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using HuntTrainAuto.Chat;
using HuntTrainAuto.Contracts;
using HuntTrainAuto.Logging;

namespace HuntTrainAuto.Services;

/// <summary>
/// ChatTwo context-menu IPC. Cannot be unit-tested without a live ChatTwo provider;
/// registration soft-fails when ChatTwo is absent and re-registers on Available.
/// </summary>
public sealed class Chat2Ipc : IChat2Service
{
	private readonly Configuration config;
	private readonly Action saveConfig;
	private readonly Action openConfigUi;
	private readonly IPluginLog? log;
	private readonly ICallGateSubscriber<string> register;
	private readonly ICallGateSubscriber<string, object?> unregister;
	private readonly ICallGateSubscriber<object?> available;
	private readonly ICallGateSubscriber<string, PlayerPayload?, ulong, Payload?, SeString?, SeString?, object?> invoke;

	private string? id;

	public Chat2Ipc(
		IDalamudPluginInterface pluginInterface,
		Configuration config,
		Action saveConfig,
		Action openConfigUi,
		IPluginLog? log = null)
	{
		this.config = config;
		this.saveConfig = saveConfig;
		this.openConfigUi = openConfigUi;
		this.log = log;

		register = pluginInterface.GetIpcSubscriber<string>("ChatTwo.Register");
		unregister = pluginInterface.GetIpcSubscriber<string, object?>("ChatTwo.Unregister");
		available = pluginInterface.GetIpcSubscriber<object?>("ChatTwo.Available");
		invoke = pluginInterface.GetIpcSubscriber<string, PlayerPayload?, ulong, Payload?, SeString?, SeString?, object?>("ChatTwo.Invoke");

		Enable();
	}

	private void Enable()
	{
		available.Subscribe(OnAvailable);
		TryRegister();
		invoke.Subscribe(OnInvoke);
	}

	private void OnAvailable() => TryRegister();

	private void TryRegister()
	{
		try
		{
			if (id != null)
			{
				try
				{
					unregister.InvokeAction(id);
				}
				catch (Exception ex)
				{
					DebugSoftFail("Unregister", ex);
				}

				id = null;
			}

			id = register.InvokeFunc();
			Debug($"registered context menu: id={id}");
		}
		catch (Exception ex)
		{
			DebugSoftFail("Register", ex);
		}
	}

	private void OnInvoke(
		string invokeId,
		PlayerPayload? sender,
		ulong contentId,
		Payload? payload,
		SeString? senderString,
		SeString? content)
	{
		if (invokeId != id)
			return;
		if (!config.ContextMenu)
			return;
		if (sender == null)
			return;

		if (ImGui.Selectable("[HTA] Set as conductor"))
		{
			ConductorList.TryAdd(config.Conductors, sender.PlayerName);
			saveConfig();
			openConfigUi();
			Debug($"conductor action: {sender.PlayerName}");
		}
	}

	public void Dispose()
	{
		if (id != null)
		{
			try
			{
				unregister.InvokeAction(id);
			}
			catch (Exception ex)
			{
				DebugSoftFail("Unregister on dispose", ex);
			}

			id = null;
		}

		invoke.Unsubscribe(OnInvoke);
		available.Unsubscribe(OnAvailable);
	}

	private void Debug(string message)
	{
		if (log != null)
			DebugBehavior.Debug(log, config.EnableDebugLogging, "Chat2", message);
	}

	private void DebugSoftFail(string operation, Exception ex)
	{
		if (log != null)
			DebugBehavior.DebugThrottled(
				log, config.EnableDebugLogging, $"chat2.{operation}", 2_000, Environment.TickCount64, "Chat2",
				$"{operation} unavailable/soft-fail: {ex.Message}");
	}
}
