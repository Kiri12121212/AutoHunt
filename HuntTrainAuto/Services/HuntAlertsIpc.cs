#nullable enable

using System;
using System.Linq;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using HuntTrainAuto.Contracts;
using HuntTrainAuto.Domain;
using HuntTrainAuto.HuntAlerts;
using HuntTrainAuto.Map;

namespace HuntTrainAuto.Services;

/// <summary>
/// Soft-fail subscriber for HuntAlerts
/// <c>HuntAlerts.OnHuntTrainMessageReceived</c> (HTA <c>SonarMonitor</c> pattern
/// via Dalamud CallGate — no ECommons EzIPC attributes).
/// Subscribes as <c>object</c> so CallGate skips JSON re-shape (publisher field-shaped
/// DTO stays intact), then copies members into <see cref="HuntTrainMessage"/>.
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
	private readonly Action? saveConfig;
	private readonly IPluginLog? log;
	private readonly ICallGateSubscriber<object, object> onHuntTrain;
	private bool subscribed;
	private HuntAlertsLastAlert? lastMappedAlert;
	private string? lastIntakeStatus;

	/// <param name="resolveMapParams">
	/// Territory → sheet map params (<c>MapManager.GetMapParams(0, territory)</c>).
	/// Null or a null return soft-falls to mapper defaults (scale 100, zero offsets).
	/// </param>
	/// <param name="resolveExVersion">
	/// Territory → <c>TerritoryType.ExVersion</c> RowId for train-group filtering.
	/// Null / failed lookup falls back to IPC <c>huntKind</c>.
	/// </param>
	/// <param name="saveConfig">
	/// Persist config after auto-adding a conductor from HA message text.
	/// Null skips save (tests / no-op).
	/// </param>
	public HuntAlertsIpc(
		IDalamudPluginInterface pluginInterface,
		Configuration config,
		Func<uint, MapCoordParams?>? resolveMapParams = null,
		Action<HuntFlag>? onFlag = null,
		Func<uint, uint?>? resolveExVersion = null,
		IPluginLog? log = null,
		Action? saveConfig = null)
	{
		this.pluginInterface = pluginInterface;
		this.config = config;
		this.resolveMapParams = resolveMapParams;
		this.resolveExVersion = resolveExVersion;
		this.onFlag = onFlag;
		this.saveConfig = saveConfig;
		this.log = log;

		onHuntTrain = pluginInterface.GetIpcSubscriber<object, object>(
			HuntAlertsAvailability.OnHuntTrainMessageReceivedChannel);

		try
		{
			onHuntTrain.Subscribe(OnHuntTrainMessageReceived);
			subscribed = true;
			log?.Information(
				$"HuntAlerts IPC subscribed ({HuntAlertsAvailability.OnHuntTrainMessageReceivedChannel})");
		}
		catch (Exception ex)
		{
			// Soft-fail: CallGate subscribe must not break plugin startup.
			subscribed = false;
			lastIntakeStatus = HuntAlertsAvailability.FormatIntakeStatus(
				"subscribe failed",
				DateTimeOffset.UtcNow);
			log?.Warning($"HuntAlerts IPC subscribe failed: {ex.Message}");
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

	/// <inheritdoc />
	public string? LastIntakeStatus => lastIntakeStatus;

	private void OnHuntTrainMessageReceived(object payload)
	{
		try
		{
			var now = DateTimeOffset.UtcNow;
			if (!HuntTrainMessageCoerce.TryCoerce(payload, out var message))
			{
				RememberIntake("rejected: bad IPC payload", now);
				log?.Warning("HuntAlerts IPC: null/unusable payload");
				return;
			}

			if (!config.HuntAlertsIntegration)
			{
				RememberIntake("rejected: integration off", now);
				log?.Information(
					$"HuntAlerts IPC ignored (integration off): type={message.huntType} kind={message.huntKind} world={message.huntWorld} territory={message.startTerritoryTypeId}");
				return;
			}

			if (!IsPluginLoaded)
			{
				RememberIntake("rejected: HuntAlerts not loaded", now);
				log?.Warning("HuntAlerts IPC ignored (plugin not loaded)");
				return;
			}

			if (!HuntAlertsAvailability.TryAcceptMessage(
				    config.HuntAlertsIntegration,
				    IsPluginLoaded,
				    message,
				    out var accepted))
			{
				RememberIntake("rejected: accept gate", now);
				log?.Information("HuntAlerts IPC ignored (accept gate)");
				return;
			}

			MapCoordParams? resolved = null;
			uint? exVersion = null;
			try
			{
				resolved = resolveMapParams?.Invoke(accepted.startTerritoryTypeId);
			}
			catch (Exception ex)
			{
				log?.Debug($"HuntAlerts map-params soft-fail: {ex.Message}");
			}

			try
			{
				exVersion = resolveExVersion?.Invoke(accepted.startTerritoryTypeId);
			}
			catch (Exception ex)
			{
				log?.Debug($"HuntAlerts ExVersion soft-fail: {ex.Message}");
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
				    out var rejectReason,
				    mapId,
				    sizeFactor,
				    offsetX,
				    offsetY,
				    trainGroupFilter: config.HuntAlertsTrainGroupFilter,
				    expansionVersion: exVersion))
			{
				RememberIntake($"rejected: {rejectReason}", now);
				log?.Information(
					$"HuntAlerts map rejected ({rejectReason}): type={accepted.huntType} kind={accepted.huntKind} world={accepted.huntWorld} territory={accepted.startTerritoryTypeId} coords='{accepted.locationCoords}' xy={accepted.mapLocationX},{accepted.mapLocationY}");
				return;
			}

			lastMappedAlert = HuntAlertsAvailability.FromMappedFlag(flag);
			RememberIntake(
				$"mapped {flag.HuntWorld ?? "?"} / {flag.PlaceName ?? $"territory {flag.TerritoryTypeId}"}",
				now);
			log?.Information(
				$"HuntAlerts mapped: {flag.HuntWorld} / {flag.PlaceName} territory={flag.TerritoryTypeId}");

			onFlag?.Invoke(flag);

			// Best-effort conductor from Message free text — after onFlag so save/parse never delays TP/nav.
			TryAutoAssignConductor(accepted.Message);
		}
		catch (Exception ex)
		{
			RememberIntake($"rejected: callback error ({ex.GetType().Name})", DateTimeOffset.UtcNow);
			log?.Warning($"HuntAlerts IPC callback soft-fail: {ex.Message}");
		}
	}

	private void RememberIntake(string detail, DateTimeOffset when)
		=> lastIntakeStatus = HuntAlertsAvailability.FormatIntakeStatus(detail, when);

	private void TryAutoAssignConductor(string? message)
	{
		if (!config.HuntAlertsAutoConductor)
			return;

		try
		{
			var result = HuntAlertsConductorDecision.Decide(
				config.Conductors,
				message,
				enabled: true);
			switch (result.Kind)
			{
				case HuntAlertsConductorAssignKind.Added:
					try
					{
						saveConfig?.Invoke();
					}
					catch (Exception ex)
					{
						log?.Debug($"HuntAlerts auto-conductor save soft-fail: {ex.Message}");
					}

					log?.Information(
						result.World == null
							? $"HuntAlerts auto-conductor: {result.Name}"
							: $"HuntAlerts auto-conductor: {result.Name} @{result.World}");
					break;
				case HuntAlertsConductorAssignKind.AlreadyPresent:
					log?.Debug($"HuntAlerts auto-conductor already present: {result.Name}");
					break;
				default:
					log?.Debug("HuntAlerts auto-conductor: no name in message");
					break;
			}
		}
		catch (Exception ex)
		{
			log?.Debug($"HuntAlerts auto-conductor soft-fail: {ex.Message}");
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
