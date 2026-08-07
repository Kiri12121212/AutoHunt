#nullable enable

using System;
using System.Linq;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using HuntTrainAuto.Contracts;
using HuntTrainAuto.Domain;
using HuntTrainAuto.HuntAlerts;
using HuntTrainAuto.Map;

namespace HuntTrainAuto.Services;

/// <summary>
/// Soft-fail subscriber for HuntAlerts
/// <c>HuntAlerts.OnHuntTrainMessageReceived</c> (HTA <c>SonarMonitor</c> pattern
/// via Dalamud CallGate — no ECommons EzIPC attributes).
/// Maps accepted messages to <see cref="HuntFlag"/> (TASKS 10.3); pipeline intake is later.
/// </summary>
public sealed class HuntAlertsIpc : IHuntAlertsService
{
	private readonly Configuration config;
	private readonly IDalamudPluginInterface pluginInterface;
	private readonly Func<uint, MapCoordParams?>? resolveMapParams;
	private readonly Action<HuntFlag>? onFlag;
	private readonly ICallGateSubscriber<HuntTrainMessage, object> onHuntTrain;
	private bool subscribed;

	/// <param name="resolveMapParams">
	/// Territory → sheet map params (<c>MapManager.GetMapParams(0, territory)</c>).
	/// Null or a null return soft-falls to mapper defaults (scale 100, zero offsets).
	/// </param>
	public HuntAlertsIpc(
		IDalamudPluginInterface pluginInterface,
		Configuration config,
		Func<uint, MapCoordParams?>? resolveMapParams = null,
		Action<HuntFlag>? onFlag = null)
	{
		this.pluginInterface = pluginInterface;
		this.config = config;
		this.resolveMapParams = resolveMapParams;
		this.onFlag = onFlag;

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

			MapCoordParams? resolved = null;
			try
			{
				resolved = resolveMapParams?.Invoke(accepted.startTerritoryTypeId);
			}
			catch
			{
				// Soft-fail: sheets / Excel access must not drop the IPC callback.
			}

			HuntTrainMessageMapper.UnpackMapParams(
				resolved,
				out var mapId,
				out var sizeFactor,
				out var offsetX,
				out var offsetY);

			if (!HuntTrainMessageMapper.TryMap(
				    accepted,
				    config.HuntAlertsIntegration,
				    config.HuntAlertsRankFilter,
				    config.HuntAlertsWorldBlacklist,
				    out var flag,
				    mapId,
				    sizeFactor,
				    offsetX,
				    offsetY))
				return;

			onFlag?.Invoke(flag);
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
