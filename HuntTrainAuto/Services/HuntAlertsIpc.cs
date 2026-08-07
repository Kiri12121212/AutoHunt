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
/// Maps accepted messages to <see cref="HuntFlag"/> (TASKS 10.3); optional
/// <paramref name="onFlag"/> runs world-visit + TP/nav intake (10.4–10.5).
/// </summary>
public sealed class HuntAlertsIpc : IHuntAlertsService
{
	private readonly Configuration config;
	private readonly IDalamudPluginInterface pluginInterface;
	private readonly Func<uint, MapCoordParams?>? resolveMapParams;
	private readonly Func<uint, uint?>? resolveExVersion;
	private readonly Action<HuntFlag>? onFlag;
	private readonly ICallGateSubscriber<HuntTrainMessage, object> onHuntTrain;
	private bool subscribed;
	private HuntAlertsLastAlert? lastMappedAlert;

	/// <param name="resolveMapParams">
	/// Territory → sheet map params (<c>MapManager.GetMapParams(0, territory)</c>).
	/// Null or a null return soft-falls to mapper defaults (scale 100, zero offsets).
	/// </param>
	/// <param name="resolveExVersion">
	/// Territory → <c>TerritoryType.ExVersion</c> RowId for train-group filtering.
	/// Null / failed lookup falls back to IPC <c>huntKind</c>.
	/// </param>
	public HuntAlertsIpc(
		IDalamudPluginInterface pluginInterface,
		Configuration config,
		Func<uint, MapCoordParams?>? resolveMapParams = null,
		Action<HuntFlag>? onFlag = null,
		Func<uint, uint?>? resolveExVersion = null)
	{
		this.pluginInterface = pluginInterface;
		this.config = config;
		this.resolveMapParams = resolveMapParams;
		this.resolveExVersion = resolveExVersion;
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

	/// <inheritdoc />
	public bool IsAvailable => PluginStatus == HuntAlertsPluginStatus.Available;

	/// <inheritdoc />
	public HuntAlertsPluginStatus PluginStatus
	{
		get
		{
			try
			{
				return HuntAlertsAvailability.Evaluate(
					pluginInterface.InstalledPlugins.Select(
						p => (p.InternalName, p.IsLoaded, (Version?)p.Version)));
			}
			catch
			{
				return HuntAlertsPluginStatus.Missing;
			}
		}
	}

	/// <inheritdoc />
	public HuntAlertsLastAlert? LastMappedAlert => lastMappedAlert;

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
			uint? exVersion = null;
			try
			{
				resolved = resolveMapParams?.Invoke(accepted.startTerritoryTypeId);
			}
			catch
			{
				// Soft-fail: sheets / Excel access must not drop the IPC callback.
			}

			try
			{
				exVersion = resolveExVersion?.Invoke(accepted.startTerritoryTypeId);
			}
			catch
			{
				// Soft-fail: ExVersion lookup must not drop the IPC callback.
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
				    offsetY,
				    trainGroupFilter: config.HuntAlertsTrainGroupFilter,
				    expansionVersion: exVersion))
				return;

			lastMappedAlert = HuntAlertsAvailability.FromMappedFlag(flag);
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
