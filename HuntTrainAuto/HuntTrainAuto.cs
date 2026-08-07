#nullable enable
using System;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using HuntTrainAuto.Services;
using HuntTrainAuto.Windows;
using Lumina.Excel.Sheets;

namespace HuntTrainAuto;

public sealed class Plugin : IDalamudPlugin
{
	private readonly IDalamudPluginInterface pluginInterface;
	private readonly IClientState clientState;
	private readonly IObjectTable objectTable;
	private readonly IDataManager dataManager;
	private readonly IFramework framework;
	private readonly ICondition condition;
	private readonly IPluginLog pluginLog;
	private readonly WindowSystem windowSystem;
	private readonly ConfigWindow configWindow;
	private readonly Chat2Ipc chat2Ipc;
	private readonly ChatMessageHandler chatMessageHandler;
	private readonly MapManager mapManager;
	private readonly TeleportPlan teleportPlan = new();
	private readonly InstanceChangeRunner instanceChange;
	private readonly MountRunner mount;
	private readonly FlagWorldHelper flagWorld;
	private readonly FlagArrivalHelper flagArrival;
	private readonly UnmountRunner unmount;

	private HuntFlag? activeHuntFlag;
	private long teleportNextAllowedMs;
	private bool isMoving;
	private Vector3 lastPosition;

	/// <summary>Teleporter IPC; execution owned by phase 3.6 Framework loop.</summary>
	public TeleporterIpc TeleporterIpc { get; }

	/// <summary>Lifestream IPC; instance/fallback TP owned by phase 3.6–3.7.</summary>
	public LifestreamIpc LifestreamIpc { get; }

	/// <summary>vnavmesh IPC; pathfind/move owned by phase 4B+.</summary>
	public VNavmeshIpc VNavmeshIpc { get; }

	/// <summary>Active Framework teleport plan (HTA <c>TeleportTo</c>).</summary>
	public TeleportPlan TeleportPlan => teleportPlan;

	public Configuration Config { get; }

	public Plugin(
		IDalamudPluginInterface pluginInterface,
		ICommandManager commandManager,
		IClientState clientState,
		IObjectTable objectTable,
		ITargetManager targetManager,
		IDataManager dataManager,
		IChatGui chatGui,
		IGameGui gameGui,
		IFramework framework,
		ICondition condition,
		IPluginLog pluginLog)
	{
		this.pluginInterface = pluginInterface;
		this.clientState = clientState;
		this.objectTable = objectTable;
		this.dataManager = dataManager;
		this.framework = framework;
		this.condition = condition;
		this.pluginLog = pluginLog;
		Config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

		windowSystem = new WindowSystem(typeof(Plugin).Assembly.GetName()?.Name ?? "HuntTrainAuto");
		configWindow = new ConfigWindow(Config, () => pluginInterface.SavePluginConfig(Config));
		windowSystem.AddWindow(configWindow);

		chat2Ipc = new Chat2Ipc(
			pluginInterface,
			Config,
			() => pluginInterface.SavePluginConfig(Config),
			() => configWindow.IsOpen = true);

		TeleporterIpc = new TeleporterIpc(pluginInterface);
		LifestreamIpc = new LifestreamIpc(pluginInterface);
		VNavmeshIpc = new VNavmeshIpc(pluginInterface);
		instanceChange = new InstanceChangeRunner(
			LifestreamIpc,
			clientState,
			objectTable,
			targetManager,
			condition,
			pluginLog);
		mount = new MountRunner(
			LifestreamIpc,
			objectTable,
			condition,
			dataManager,
			pluginLog,
			() => instanceChange.IsActive);
		flagWorld = new FlagWorldHelper(VNavmeshIpc);
		flagArrival = new FlagArrivalHelper(VNavmeshIpc);
		unmount = new UnmountRunner(
			VNavmeshIpc,
			objectTable,
			condition,
			pluginLog,
			() => teleportPlan.Active != null,
			() => instanceChange.IsActive);
		mapManager = new MapManager(dataManager, msg => pluginLog.Warning(msg));

		chatMessageHandler = new ChatMessageHandler(chatGui, gameGui, Config);
		chatMessageHandler.TryGetPlayerSnapshot = TryGetPlayerSnapshot;
		chatMessageHandler.HuntFlagReceived += OnHuntFlagReceived;

		pluginInterface.UiBuilder.Draw += Draw;
		pluginInterface.UiBuilder.OpenConfigUi += ToggleUi;
		clientState.TerritoryChanged += OnTerritoryChanged;
		framework.Update += OnFrameworkUpdate;

		commandManager.AddHandler("/hta", new CommandInfo(OnCommand)
		{
			HelpMessage = "Toggle HuntTrainAuto UI; /hta clear; /hta add <name> or /hta <name>",
		});
	}

	public string Name => "HuntTrainAuto";

	private void OnCommand(string command, string args) =>
		ChatCommands.Handle(
			args,
			Config.Conductors,
			ToggleUi,
			() => configWindow.IsOpen = true,
			() => pluginInterface.SavePluginConfig(Config));

	private void OnHuntFlagReceived(HuntFlag flag)
	{
		// New flag (skip or new plan) invalidates any in-flight instance change / automove / mount / unmount.
		activeHuntFlag = flag;
		instanceChange.Clear();
		mount.Clear();
		flagArrival.Clear();
		unmount.ClearAll();
		if (!teleportPlan.TryAdoptFromIntent(chatMessageHandler.TeleportIntent))
		{
			// Same-zone close enough: no TP — still mount before later nav (HTA mount-on-ready).
			if (chatMessageHandler.TeleportIntent.LatestDecision is
				{ Action: TeleportAction.Skip, SkipReason: TeleportSkipReason.AlreadyClose })
				mount.EnqueueIfEnabled(Config.UseMount);
			return;
		}

		ApplyDelayTeleport();
		pluginLog.Information("Engaging autoteleport");
	}

	private void ApplyDelayTeleport()
	{
		var span = Config.TeleportDelayMax - Config.TeleportDelayMin;
		var offset = span > 0 ? Random.Shared.Next(span) : 0;
		teleportNextAllowedMs = TeleportThrottle.ApplyPreDelay(
			teleportNextAllowedMs,
			Environment.TickCount64,
			Config.TeleportDelayEnabled,
			Config.TeleportDelayMin,
			Config.TeleportDelayMax,
			offset);
	}

	private void OnTerritoryChanged(uint territoryId)
	{
		if (teleportPlan.Active is { } plan)
		{
			if (TeleportGate.ShouldEnqueueInstanceChange(plan.Instance) && territoryId == plan.Territory)
				EnqueueChangeInstanceAfterTeleport(plan.Instance, plan.Territory);

			// HTA: TaskMount.EnqueueIfEnabled after TeleportTo clear (waits for instance idle).
			mount.EnqueueIfEnabled(Config.UseMount);
			pluginLog.Debug("TeleportPlan cleared (territory changed)");
			teleportPlan.Clear();
		}

		if (HuntingTerritory.IsHuntingTerritory(territoryId, GetIntendedUseRowId))
			return;

		instanceChange.Clear();
		mount.Clear();
		activeHuntFlag = null;
		flagArrival.Clear();
		unmount.ClearAll();
		ConductorList.Clear(Config.Conductors);
		pluginInterface.SavePluginConfig(Config);
	}

	private void OnFrameworkUpdate(IFramework fw)
	{
		_ = fw;
		var now = Environment.TickCount64;
		var player = objectTable.LocalPlayer;
		var active = teleportPlan.Active;

		if (TeleportGate.IsPlayerReady(
				player != null,
				player is { CurrentHp: > 0 },
				condition[ConditionFlag.Unconscious])
			&& active != null
			&& TeleportGate.IsAutoTeleportEnabled(Config.Enabled, Config.AutoTeleport))
		{
			if (TeleportGate.IsScreenReady(
				condition[ConditionFlag.BetweenAreas],
				condition[ConditionFlag.BetweenAreas51],
				condition[ConditionFlag.OccupiedInCutSceneEvent],
				condition[ConditionFlag.WatchingCutscene]))
			{
				var soft = TeleportThrottle.SoftWaitNextAllowed(
					teleportNextAllowedMs,
					now,
					player!.IsCasting,
					player.CastActionId,
					condition[ConditionFlag.Casting],
					condition[ConditionFlag.MountOrOrnamentTransition]);
				if (soft != null)
					teleportNextAllowedMs = soft.Value;

				if (TeleportGate.CanAttemptTeleport(
					condition[ConditionFlag.InCombat],
					condition[ConditionFlag.BetweenAreas],
					condition[ConditionFlag.BetweenAreas51],
					condition[ConditionFlag.Casting],
					isMoving))
				{
					if (TeleportThrottle.TryFire(ref teleportNextAllowedMs, now))
						TryExecuteTeleport(active.AetheryteId);
				}
			}

			isMoving = player!.Position != lastPosition;
			lastPosition = player.Position;
		}

		if (TeleportGate.IsBetweenAreas(
			condition[ConditionFlag.BetweenAreas],
			condition[ConditionFlag.BetweenAreas51])
			&& teleportPlan.Active is { } betweenPlan)
		{
			if (TeleportGate.ShouldEnqueueInstanceChange(betweenPlan.Instance))
				EnqueueChangeInstanceAfterTeleport(betweenPlan.Instance, betweenPlan.Territory);

			mount.EnqueueIfEnabled(Config.UseMount);
			pluginLog.Debug("TeleportPlan cleared (between areas)");
			teleportPlan.Clear();
		}

		instanceChange.Tick();
		// Arrival/unmount before mount.Tick so AlreadyClose mount jobs are cleared before they remount.
		TickFlagArrivalAndUnmount();
		mount.Tick(Config.Mount);
	}

	/// <summary>
	/// Resolve flag world pos (retry PointOnFloor), detect arrival, enqueue TaskUnmount.
	/// Minimal phase-4 handoff — full nav/follow orchestrator is phase 5/7.
	/// </summary>
	private void TickFlagArrivalAndUnmount()
	{
		var flag = activeHuntFlag;
		var player = objectTable.LocalPlayer;
		if (flag == null || player == null)
			return;

		if (flag.WorldPos is null || flag.WorldPos == Vector3.Zero)
			flagWorld.TryResolve(flag);

		var arrival = flagArrival.Tick(player.Position, flag.WorldPos, Config.FlagArrivalTolerance);
		// Already at flag (e.g. AlreadyClose skip): cancel pending mount so it cannot remount after unmount.
		if (arrival.IsArrived)
			mount.Clear();
		unmount.EnqueueOnArrivalIfEnabled(Config.AutoUnmountAtFlag, arrival.IsArrived);
		unmount.Tick(flagArrival.PathStoppedForArrival, arrival.IsArrived);
	}

	private void TryExecuteTeleport(uint aetheryteId)
	{
		if (TeleporterIpc.Teleport(aetheryteId, 0))
		{
			pluginLog.Information("Teleporting using Teleporter plugin");
			return;
		}

		if (LifestreamIpc.Teleport(aetheryteId))
		{
			pluginLog.Information("Teleporting using Lifestream plugin");
			return;
		}

		pluginLog.Warning("Failed to teleport (Teleporter/Lifestream unavailable or congested); will retry");
	}

	/// <summary>
	/// Enqueue post-TP instance switch (HTA <c>TaskChangeInstanceAfterTeleport</c>).
	/// Survives <see cref="TeleportPlan"/> clear; advanced on Framework ticks.
	/// </summary>
	private void EnqueueChangeInstanceAfterTeleport(int instance, uint territoryId)
		=> instanceChange.Enqueue(instance, territoryId);

	private TeleportPlayerSnapshot? TryGetPlayerSnapshot(HuntFlag flag)
	{
		try
		{
			var player = objectTable.LocalPlayer;
			if (player == null)
				return null;

			var mapParams = mapManager.GetMapParams(flag.MapId, flag.TerritoryTypeId);
			float? flagMapX = null;
			float? flagMapY = null;
			NearestAetheryteResult? nearest = null;

			if (mapParams != null)
			{
				var sizeFactor = mapParams.Value.SizeFactor;
				flagMapX = MapCoordinates.ConvertRawPositionToMapCoordinate(flag.RawX, sizeFactor);
				flagMapY = MapCoordinates.ConvertRawPositionToMapCoordinate(flag.RawY, sizeFactor);
				nearest = mapManager.GetNearestAetheryte(
					flag.TerritoryTypeId,
					flag.MapId,
					flagMapX.Value,
					flagMapY.Value,
					Config.AetheryteBlacklist,
					Config.DistanceCompensationHack);
			}

			float? distance = null;
			if (mapParams != null
				&& flagMapX != null
				&& flagMapY != null
				&& clientState.TerritoryType == flag.TerritoryTypeId)
			{
				var pos = player.Position;
				var p = mapParams.Value;
				var px = MapCoordinates.ConvertWorldToMapCoordinate(pos.X, p.SizeFactor, p.OffsetX);
				var py = MapCoordinates.ConvertWorldToMapCoordinate(pos.Z, p.SizeFactor, p.OffsetY);
				distance = MapCoordinates.MapDistance(px, py, flagMapX.Value, flagMapY.Value);
			}

			var lifestreamInstance = LifestreamIpc.GetCurrentInstance();
			return new TeleportPlayerSnapshot
			{
				CurrentTerritory = clientState.TerritoryType,
				CurrentInstance = lifestreamInstance > 0 ? lifestreamInstance : (int)clientState.Instance,
				TargetInstance = 0,
				PlayerDistance = distance,
				Nearest = nearest,
			};
		}
		catch (Exception ex)
		{
			pluginLog.Debug($"TryGetPlayerSnapshot soft-fail: {ex.Message}");
			return null;
		}
	}

	private uint? GetIntendedUseRowId(uint territoryId) =>
		dataManager.GetExcelSheet<TerritoryType>()?.GetRowOrDefault(territoryId)?.TerritoryIntendedUse.RowId;

	private void Draw() => windowSystem.Draw();

	private void ToggleUi()
	{
		configWindow.IsOpen = !configWindow.IsOpen;
		pluginInterface.SavePluginConfig(Config);
	}

	public void Dispose()
	{
		framework.Update -= OnFrameworkUpdate;
		clientState.TerritoryChanged -= OnTerritoryChanged;
		chatMessageHandler.HuntFlagReceived -= OnHuntFlagReceived;
		instanceChange.Clear();
		mount.Clear();
		activeHuntFlag = null;
		flagArrival.Clear();
		unmount.ClearAll();
		chatMessageHandler.Dispose();
		VNavmeshIpc.Dispose();
		LifestreamIpc.Dispose();
		TeleporterIpc.Dispose();
		chat2Ipc.Dispose();
		pluginInterface.UiBuilder.Draw -= Draw;
		pluginInterface.UiBuilder.OpenConfigUi -= ToggleUi;
		windowSystem.RemoveAllWindows();
		configWindow.Dispose();
	}
}
