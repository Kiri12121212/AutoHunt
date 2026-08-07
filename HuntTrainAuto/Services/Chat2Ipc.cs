#nullable enable
using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;

namespace HuntTrainAuto.Services;

/// <summary>
/// ChatTwo context-menu IPC. Cannot be unit-tested without a live ChatTwo provider;
/// registration soft-fails when ChatTwo is absent and re-registers on Available.
/// </summary>
public sealed class Chat2Ipc : IDisposable
{
	private readonly Configuration config;
	private readonly Action saveConfig;
	private readonly Action openConfigUi;
	private readonly ICallGateSubscriber<string> register;
	private readonly ICallGateSubscriber<string, object?> unregister;
	private readonly ICallGateSubscriber<object?> available;
	private readonly ICallGateSubscriber<string, PlayerPayload?, ulong, Payload?, SeString?, SeString?, object?> invoke;

	private string? id;

	public Chat2Ipc(
		IDalamudPluginInterface pluginInterface,
		Configuration config,
		Action saveConfig,
		Action openConfigUi)
	{
		this.config = config;
		this.saveConfig = saveConfig;
		this.openConfigUi = openConfigUi;

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
				catch
				{
					// Previous registration may already be gone after ChatTwo reload.
				}

				id = null;
			}

			id = register.InvokeFunc();
		}
		catch
		{
			// ChatTwo may be absent; Available will re-register when it loads.
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
			catch
			{
				// ChatTwo may already be unloaded.
			}

			id = null;
		}

		invoke.Unsubscribe(OnInvoke);
		available.Unsubscribe(OnAvailable);
	}
}
