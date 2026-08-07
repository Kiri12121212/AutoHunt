#nullable enable

using System;
using System.Linq;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using HuntTrainAuto.Contracts;
using HuntTrainAuto.HuntAlerts;

namespace HuntTrainAuto.Services;

/// <summary>
/// Soft-fail subscriber for HuntAlerts
/// <c>HuntAlerts.OnHuntTrainMessageReceived</c> (HTA <c>SonarMonitor</c> pattern
/// via Dalamud CallGate — no ECommons EzIPC attributes).
/// Receiving an event is a no-op / thin hook until TASKS 10.3 mapping.
/// </summary>
public sealed class HuntAlertsIpc : IHuntAlertsService
{
	private readonly Configuration config;
	private readonly IDalamudPluginInterface pluginInterface;
	private readonly Action<HuntTrainMessage>? onMessage;
	private readonly ICallGateSubscriber<HuntTrainMessage, object> onHuntTrain;
	private bool subscribed;

	public HuntAlertsIpc(
		IDalamudPluginInterface pluginInterface,
		Configuration config,
		Action<HuntTrainMessage>? onMessage = null)
	{
		this.pluginInterface = pluginInterface;
		this.config = config;
		this.onMessage = onMessage;

		onHuntTrain = pluginInterface.GetIpcSubscriber<HuntTrainMessage, object>(
			HuntAlertsAvailability.OnHuntTrainMessageReceivedChannel);

		try
		{
			onHuntTrain.Subscribe(OnHuntTrainMessageReceived);
			subscribed = true;
		}
		catch
		{
			// Soft-fail: CallGate subscribe must not break plugin startup.
			subscribed = false;
		}
	}

	/// <inheritdoc />
	public bool IsPluginLoaded
	{
		get
		{
			try
			{
				return HuntAlertsAvailability.IsPluginLoaded(
					pluginInterface.InstalledPlugins.Select(p => (p.InternalName, p.IsLoaded)));
			}
			catch
			{
				return false;
			}
		}
	}

	private void OnHuntTrainMessageReceived(HuntTrainMessage message)
	{
		try
		{
			if (!HuntAlertsAvailability.TryAcceptMessage(
				    config.HuntAlertsIntegration,
				    IsPluginLoaded,
				    message,
				    out var accepted))
				return;

			onMessage?.Invoke(accepted);
		}
		catch
		{
			// Never throw out of an IPC callback.
		}
	}

	public void Dispose()
	{
		if (!subscribed)
			return;

		try
		{
			onHuntTrain.Unsubscribe(OnHuntTrainMessageReceived);
		}
		catch
		{
			// HuntAlerts / CallGate may already be gone.
		}

		subscribed = false;
	}
}
