#nullable enable
using System;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
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
	private readonly IChatOutput chat;
	private readonly Chat2Ipc chat2Ipc;
	private readonly HuntAlertsIpc huntAlertsIpc;
	private readonly ChatMessageHandler chatMessageHandler;
	private readonly MapManager mapManager;
	private readonly TeleportPlan teleportPlan = new();
	private readonly InstanceChangeRunner instanceChange;
	private readonly MountRunner mount;
	private readonly FlagWorldHelper flagWorld;
	private readonly FlagArrivalHelper flagArrival;
	private readonly UnmountRunner unmount;
	private readonly MovementHelper movement;
	private readonly EngageTargetHelper engage;
	private readonly FollowHelper follow;
	private readonly CombatTransitionHelper combat;
	private readonly RsrEnableHelper rsrEnable;
	private readonly HuntTrainController train = new();
	private readonly DebugEventLog debugLog = new();
	private readonly DebugEventProbe debugProbe;
	private readonly HuntNotificator notificator;

	private HuntFlag? activeHuntFlag;
	private long teleportNextAllowedMs;
	private bool isMoving;
	private Vector3 lastPosition;

	/// <summary>Teleporter IPC; execution owned by phase 3.6 Framework loop.</summary>
	public ITeleporterService TeleporterIpc { get; }

	/// <summary>Lifestream IPC; instance/fallback TP owned by phase 3.6–3.7.</summary>
	public ILifestreamService LifestreamIpc { get; }

	/// <summary>vnavmesh IPC; pathfind/move owned by phase 4B+.</summary>
	public IVnavmeshService VNavmeshIpc { get; }

	/// <summary>Rotation Solver Reborn IPC; enable gated by phase 6.2.</summary>
	public IRsrService RsrIpc { get; }

	/// <summary>
	/// Combat/follow phase latch (TASKS 5.8–5.9). Phase 6.2 edge-triggers
	/// RSR from <see cref="CombatSession.InCombatPhase"/>.
	/// </summary>
	public CombatSession CombatSession => combat.Session;

	/// <summary>True while party-engage combat phase is active (RSR enable signal).</summary>
	public bool InCombatPhase => combat.InCombatPhase;

	/// <summary>
	/// Train pipeline state machine (TASKS 7.1–7.4).
	/// Framework.Update fills progress signals via <see cref="HuntTrainObserve"/> and ticks phases;
	/// new flags abort+restart via <see cref="FlagRestartDecision"/>; resets on master-off / hard clear / dispose.
	/// </summary>
	public HuntTrainController Train => train;

	/// <summary>Current <see cref="HuntTrainController.Phase"/>.</summary>
	public HuntTrainPhase TrainPhase => train.Phase;

	/// <summary>
	/// Read-only Status panel snapshot (TASKS 8.6): phase, mount, follow target, nav.
	/// Soft-fails individual probes; never throws to UI.
	/// </summary>
	public StatusSnapshot CaptureStatus()
	{
		var mounted = false;
		try
		{
			mounted = condition[ConditionFlag.Mounted];
		}
		catch
		{
			// soft-fail
		}

		string? followName = null;
		var followEnabled = false;
		try
		{
			followEnabled = follow.Enabled;
			var target = follow.FollowTarget;
			if (target != null)
				followName = target.Name.TextValue;
		}
		catch
		{
			// soft-fail
		}

		var pathRunning = false;
		var waypoints = 0;
		var pathfindInProgress = false;
		try
		{
			pathRunning = VNavmeshIpc.PathIsRunning();
			waypoints = VNavmeshIpc.PathNumWaypoints();
			pathfindInProgress = VNavmeshIpc.SimpleMovePathfindInProgress();
		}
		catch
		{
			// soft-fail
		}

		return new StatusSnapshot
		{
			Phase = train.Phase,
			Mounted = mounted,
			MountPipeline = mount.Session.Phase,
			UnmountPipeline = unmount.Session.Phase,
			FollowTargetName = followName,
			FollowEnabled = followEnabled,
			NavPathRunning = pathRunning,
			NavWaypoints = waypoints,
			NavPathfindInProgress = pathfindInProgress,
		};
	}

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
		IPartyList partyList,
		IPluginLog pluginLog,
		INotificationManager notificationManager)
	{
		this.pluginInterface = pluginInterface;
		this.clientState = clientState;
		this.objectTable = objectTable;
		this.dataManager = dataManager;
		this.framework = framework;
		this.condition = condition;
		this.pluginLog = pluginLog;
		Config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
		Config.EngageRange = CombatDecision.ClampEngageRange(Config.EngageRange);
		Config.ARankScanRange = EngageTargetDecision.ClampARankScanRange(Config.ARankScanRange);
		Config.RsrHostileType = RsrSettingsDecision.ClampHostileType(Config.RsrHostileType);
		Config.RsrTargetingTank = RsrSettingsDecision.ClampTargetingType(
			Config.RsrTargetingTank,
			RsrSettingsDecision.DefaultTankTargeting);
		Config.RsrTargetingNonTank = RsrSettingsDecision.ClampTargetingType(
			Config.RsrTargetingNonTank,
			RsrSettingsDecision.DefaultNonTankTargeting);

		windowSystem = new WindowSystem(typeof(Plugin).Assembly.GetName()?.Name ?? "HuntTrainAuto");

		TeleporterIpc = new TeleporterIpc(pluginInterface);
		LifestreamIpc = new LifestreamIpc(pluginInterface);
		VNavmeshIpc = new VNavmeshIpc(pluginInterface);
		RsrIpc = new RsrIpc(pluginInterface);

		debugProbe = new DebugEventProbe(debugLog);
		notificator = new HuntNotificator(
			notificationManager,
			pluginLog,
			() => Config.Enabled,
			() => Config.EnableNotifications,
			() => Config.EnableNotificationSound,
			PlayNotificationSound);

		configWindow = new ConfigWindow(
			Config,
			() => pluginInterface.SavePluginConfig(Config),
			() => TeleporterIpc.IsAvailable,
			() => LifestreamIpc.IsAvailable,
			() => VNavmeshIpc.IsAvailable,
			() => RsrIpc.IsAvailable,
			CaptureStatus,
			debugLog);
		windowSystem.AddWindow(configWindow);

		chat = new GameChat();
		chat2Ipc = new Chat2Ipc(
			pluginInterface,
			Config,
			() => pluginInterface.SavePluginConfig(Config),
			() => configWindow.IsOpen = true);
		// MapManager before HuntAlerts IPC so train messages resolve SizeFactor/offsets.
		mapManager = new MapManager(dataManager, msg => pluginLog.Warning(msg));
		// HuntAlerts → HuntFlag mapping + cross-world Lifestream (TASKS 10.3/10.4);
		// same-world TP/nav intake is 10.5.
		huntAlertsIpc = new HuntAlertsIpc(
			pluginInterface,
			Config,
			territoryTypeId => mapManager.GetMapParams(mapId: 0, territoryTypeId),
			OnHuntAlertsFlag);
		instanceChange = new InstanceChangeRunner(
			LifestreamIpc,
			chat,
			clientState,
			objectTable,
			targetManager,
			condition,
			pluginLog);
		mount = new MountRunner(
			LifestreamIpc,
			chat,
			objectTable,
			condition,
			dataManager,
			pluginLog,
			() => instanceChange.IsActive);
		flagWorld = new FlagWorldHelper(VNavmeshIpc);
		flagArrival = new FlagArrivalHelper(VNavmeshIpc);
		unmount = new UnmountRunner(
			VNavmeshIpc,
			chat,
			objectTable,
			condition,
			pluginLog,
			() => teleportPlan.Active != null,
			() => instanceChange.IsActive);
		movement = new MovementHelper(
			VNavmeshIpc,
			chat,
			objectTable,
			dataManager,
			condition,
			clientState,
			pluginLog);
		engage = new EngageTargetHelper(
			objectTable,
			partyList,
			targetManager,
			dataManager,
			pluginLog,
			movement,
			() => Config.EngageRange,
			() => Config.ARankScanRange);
		// Retained for CombatTransitionHelper Clear API only — party follow is disabled.
		follow = new FollowHelper(
			VNavmeshIpc,
			objectTable,
			pluginLog,
			() => Config.PartyFollowDistance);
		combat = new CombatTransitionHelper(
			objectTable,
			partyList,
			condition,
			pluginLog,
			() => Config.EngageRange);
		rsrEnable = new RsrEnableHelper(RsrIpc, pluginLog, ResolveRsrRotationSettings);

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

	/// <summary>
	/// HuntAlerts mapped-flag hook (TASKS 10.4): cross-world → Lifestream <c>ChangeWorld</c>.
	/// Same-world is left for controller intake (10.5).
	/// </summary>
	private void OnHuntAlertsFlag(HuntFlag flag)
	{
		try
		{
			var decision = HuntAlertsWorldVisit.TryHandle(
				flag,
				Config.HuntAlertsIntegration,
				LifestreamIpc,
				TryGetCurrentWorldName());

			if (decision.Action == HuntAlertsWorldVisitAction.RequestWorldVisit)
				pluginLog.Information($"HuntAlerts cross-world visit: {decision.World}");
			else if (decision.Action == HuntAlertsWorldVisitAction.CannotVisit)
				pluginLog.Information($"HuntAlerts cannot visit world: {decision.World}");
		}
		catch (Exception ex)
		{
			pluginLog.Debug($"OnHuntAlertsFlag soft-fail: {ex.Message}");
		}
	}

	private string? TryGetCurrentWorldName()
	{
		try
		{
			var player = objectTable.LocalPlayer;
			if (player == null || !player.CurrentWorld.IsValid)
				return null;

			var name = player.CurrentWorld.Value.Name.ToString();
			return string.IsNullOrWhiteSpace(name) ? null : name;
		}
		catch
		{
			return null;
		}
	}

	private void OnHuntFlagReceived(HuntFlag flag)
	{
		// New flag: abort mid-pipeline if needed, then restart (TASKS 7.4).
		// Snapshot pipeline before clears / adopt so Start* is not applied while non-Idle.
		var pipelineActive = FlagRestartDecision.IsPipelineActive(
			train.Phase,
			HasInFlightPipelineWork());

		activeHuntFlag = flag;
		notificator.NotifyConductorFlag(flag);
		debugProbe.RecordFlagReceived(Config.EnableDebugLogging, flag.PlaceName);

		var adopted = teleportPlan.TryAdoptFromIntent(chatMessageHandler.TeleportIntent);
		var alreadyClose = chatMessageHandler.TeleportIntent.LatestDecision is
			{ Action: TeleportAction.Skip, SkipReason: TeleportSkipReason.AlreadyClose };

		var plan = FlagRestartDecision.Decide(
			Config.Enabled,
			pipelineActive,
			teleportPlan.HasActive,
			alreadyClose,
			Config.UseMount);

		ApplyFlagRestart(plan);
		// Probe post-abort Idle/clears before Start* so mid-pipeline restarts do not
		// collapse Combat→Teleport (etc.) into one impossible edge.
		ObserveDebugSignals();

		if (adopted)
		{
			ApplyDelayTeleport();
			pluginLog.Information("Engaging autoteleport");
		}
		else if (alreadyClose)
		{
			// Same-zone close enough: no TP — still mount before later nav (HTA mount-on-ready).
			// Enqueue after ClearMount so AbortThenRestart does not wipe this job.
			mount.EnqueueIfEnabled(Config.UseMount);
		}

		if (plan.StartEvent != HuntTrainEvent.None)
			train.Apply(plan.StartEvent);

		ObserveDebugSignals();
	}

	/// <summary>
	/// Leftover runners / path while phase may already be Idle (soft-fail IPC).
	/// </summary>
	private bool HasInFlightPipelineWork()
	{
		try
		{
			if (teleportPlan.HasActive
				|| instanceChange.IsActive
				|| mount.IsActive
				|| unmount.IsActive
				|| combat.InCombatPhase
				|| follow.Enabled)
				return true;

			return VNavmeshIpc.PathIsRunning();
		}
		catch
		{
			// Soft-fail: treat as no leftover work; phase alone still drives abort.
			return false;
		}
	}

	/// <summary>
	/// Apply <see cref="FlagRestartDecision"/> abort clears (TASKS 7.4).
	/// Mount enqueue / Start* Apply happen in <see cref="OnHuntFlagReceived"/> after this.
	/// </summary>
	private void ApplyFlagRestart(FlagRestartPlan plan)
	{
		if (plan.StopNavPath)
		{
			try
			{
				movement.Stop();
			}
			catch
			{
				// soft-fail: vnav / player may be unavailable
			}
		}

		if (plan.ClearFollow)
			follow.Clear();
		if (plan.ClearInstanceChange)
			instanceChange.Clear();
		if (plan.ClearMount)
			mount.Clear();
		if (plan.ClearFlagArrival)
			flagArrival.Clear();
		if (plan.ClearUnmount)
			unmount.ClearAll();
		if (plan.ClearEngage)
			engage.Clear();
		if (plan.ClearCombat)
			combat.Clear();
		if (plan.ClearRsr)
		{
			// RSR stop: RsrStopTrigger.FlagChange → ImmediateClear.
			rsrEnable.Clear();
		}

		if (plan.ResetTrainController)
			train.Reset();
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
		try
		{
			var hunting = HuntingTerritory.IsHuntingTerritory(territoryId, GetIntendedUseRowId);
			var plan = TerritoryCleanupDecision.Decide(teleportPlan.HasActive, hunting);
			ApplyTerritoryCleanup(territoryId, plan);
		}
		catch (Exception ex)
		{
			pluginLog.Debug($"OnTerritoryChanged soft-fail: {ex.Message}");
		}
	}

	/// <summary>
	/// Apply <see cref="TerritoryCleanupDecision"/> flags (TASKS 7.3).
	/// Dismount-safe: stop-path + clear jobs; do not force dismount mid-zone-load.
	/// </summary>
	private void ApplyTerritoryCleanup(uint territoryId, TerritoryCleanupPlan plan)
	{
		if (plan.Kind == TerritoryCleanupKind.StayHuntingNoop)
			return;

		// Snapshot plan before ClearTeleportPlan so instance enqueue still sees destination.
		var activePlan = teleportPlan.Active;

		// Stale session / path first — then TP handoff enqueue (mount must not be cleared after).
		if (plan.StopNavPath)
		{
			try
			{
				movement.Stop();
			}
			catch
			{
				// soft-fail: vnav / player may be unavailable mid-load
			}
		}

		if (plan.ClearFollow)
			follow.Clear();
		if (plan.ClearFlagArrival)
			flagArrival.Clear();
		if (plan.ClearUnmount)
			unmount.ClearAll();
		if (plan.ClearEngage)
			engage.Clear();
		if (plan.ClearCombat)
			combat.Clear();
		if (plan.ClearRsr)
		{
			// RSR stop: RsrStopTrigger.TerritoryLeave → ImmediateClear (leave);
			// TP-arrival also clears any stale rotation latch from the previous zone.
			rsrEnable.Clear();
		}

		if (plan.EnqueueInstanceChangeIfNeeded && activePlan is { } tp)
		{
			if (TeleportGate.ShouldEnqueueInstanceChange(tp.Instance) && territoryId == tp.Territory)
				EnqueueChangeInstanceAfterTeleport(tp.Instance, tp.Territory);
		}

		if (plan.EnqueueMount)
			mount.EnqueueIfEnabled(Config.UseMount);

		if (plan.ClearTeleportPlan)
		{
			pluginLog.Debug(
				plan.Kind == TerritoryCleanupKind.TpArrivalHandoff
					? "TeleportPlan cleared (TP arrival handoff)"
					: "TeleportPlan cleared (territory leave)");
			teleportPlan.Clear();
		}

		if (plan.ClearInstanceChange)
			instanceChange.Clear();
		if (plan.ClearMount)
			mount.Clear();
		if (plan.ClearActiveHuntFlag)
			activeHuntFlag = null;
		if (plan.ClearConductors)
			ConductorList.Clear(Config.Conductors);
		if (plan.ResetTrainController)
			train.Reset();
		if (plan.SaveConfig)
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
		if (!Config.Enabled)
		{
			// RSR stop: RsrStopTrigger.MasterOff → ImmediateClear (Tick skipped below).
			mount.Clear();
			unmount.ClearAll();
			engage.Clear();
			combat.Clear();
			rsrEnable.Clear();
			train.Reset();
			ObserveDebugSignals();
			return;
		}

		// Navigate: path toward flag world pos (existing MovementHelper).
		if (train.Phase == HuntTrainPhase.Navigate)
			TickNavigateToFlag();

		// Arrival/unmount before mount.Tick so AlreadyClose mount jobs are cleared before they remount.
		var withinArrival = TickFlagArrivalAndUnmount();
		mount.Tick(Config.Mount);
		// After unmount: join conductor fight or path to nearby A-rank (never follow players).
		var playerDead = IsLocalPlayerDead();
		if (unmount.ReadyForGroundFollow && !playerDead)
		{
			engage.Tick(
				combat.Session,
				Config.Conductors,
				pluginEnabled: true,
				playerDead: false);
		}

		// Death / mob-dead / combat-end → CombatDecision Idle (RsrStopPath.CombatPhaseTick).
		// Enter combat is owned by EngageTargetHelper.
		combat.Tick(follow, pluginEnabled: true);
		// RSR start only in combat phase; Stop on phase exit (death / combat end) via DecideTick.
		rsrEnable.Tick(combat.InCombatPhase);

		// Advance HuntTrainController from live runner signals (one event per tick).
		train.Tick(HuntTrainObserve.BuildProgressSnapshot(
			pluginEnabled: true,
			abort: false,
			teleportPlanActive: teleportPlan.HasActive,
			mountJobActive: mount.IsActive,
			withinFlagArrival: withinArrival,
			readyForGroundFollow: unmount.ReadyForGroundFollow,
			inCombatPhase: combat.InCombatPhase));

		ObserveDebugSignals();
	}

	/// <summary>
	/// Edge-record phase / mount / unmount / follow for the Debug tab (TASKS 9.2).
	/// Soft-fails individual probes; never throws to Framework.
	/// </summary>
	private void ObserveDebugSignals()
	{
		try
		{
			string? followName = null;
			var followEnabled = false;
			try
			{
				followEnabled = follow.Enabled;
				var target = follow.FollowTarget;
				if (target != null)
					followName = target.Name.TextValue;
			}
			catch
			{
				// soft-fail follow probe
			}

			debugProbe.Observe(
				Config.EnableDebugLogging,
				train.Phase,
				mount.Session.Phase,
				unmount.Session.Phase,
				followName,
				followEnabled);
		}
		catch (Exception ex)
		{
			pluginLog.Debug($"ObserveDebugSignals soft-fail: {ex.Message}");
		}
	}

	/// <summary>Optional UI sound for conductor-flag alerts; soft-fail via caller.</summary>
	private static unsafe void PlayNotificationSound()
		=> UIGlobals.PlaySoundEffect(36);

	private bool IsLocalPlayerDead()
	{
		try
		{
			if (condition[ConditionFlag.Unconscious])
				return true;
			var player = objectTable.LocalPlayer;
			return player is { CurrentHp: <= 0 };
		}
		catch
		{
			return false;
		}
	}

	/// <summary>
	/// Resolve RSR targeting / HostileType from config + local ClassJob.Role (tank = 1).
	/// Soft-fails to non-tank defaults when the player/job is unavailable.
	/// </summary>
	private (RsrTargetingType Targeting, RsrTargetHostileType Hostile) ResolveRsrRotationSettings()
	{
		var isTank = false;
		try
		{
			var player = objectTable.LocalPlayer;
			if (player != null && player.ClassJob.ValueNullable is { } job)
				isTank = RsrSettingsDecision.IsTankRole(job.Role);
		}
		catch
		{
			// Soft-fail: treat as non-tank.
		}

		return RsrSettingsDecision.Resolve(
			isTank,
			Config.RsrHostileType,
			Config.RsrTargetingTank,
			Config.RsrTargetingNonTank);
	}

	/// <summary>
	/// While <see cref="HuntTrainPhase.Navigate"/>: resolve flag world pos and path with MovementHelper.
	/// Soft-fails when player/flag/world pos missing.
	/// </summary>
	private void TickNavigateToFlag()
	{
		try
		{
			var flag = activeHuntFlag;
			var player = objectTable.LocalPlayer;
			if (flag == null || player == null)
				return;

			if (flag.WorldPos is null || flag.WorldPos == Vector3.Zero)
				flagWorld.TryResolve(flag);

			if (flag.WorldPos is not { } pos || pos == Vector3.Zero)
				return;

			movement.MovePreferFlyWhenMounted(
				pos,
				tolerance: MovementDecision.DefaultTolerance,
				lastPointTolerance: Config.FlagArrivalTolerance);
		}
		catch (Exception ex)
		{
			pluginLog.Debug($"TickNavigateToFlag soft-fail: {ex.Message}");
		}
	}

	/// <summary>
	/// Resolve flag world pos (retry PointOnFloor), detect arrival, enqueue TaskUnmount.
	/// Returns whether the player is within flag arrival tolerance this tick.
	/// </summary>
	private bool TickFlagArrivalAndUnmount()
	{
		try
		{
			var flag = activeHuntFlag;
			var player = objectTable.LocalPlayer;
			if (flag == null || player == null)
				return false;

			if (flag.WorldPos is null || flag.WorldPos == Vector3.Zero)
				flagWorld.TryResolve(flag);

			var arrival = flagArrival.Tick(player.Position, flag.WorldPos, Config.FlagArrivalTolerance);
			// Already at flag (e.g. AlreadyClose skip): cancel pending mount so it cannot remount after unmount.
			if (arrival.IsArrived)
				mount.Clear();
			unmount.EnqueueOnArrivalIfEnabled(Config.AutoUnmountAtFlag, arrival.IsArrived);
			unmount.Tick(flagArrival.PathStoppedForArrival, arrival.IsArrived);
			return arrival.IsArrived;
		}
		catch (Exception ex)
		{
			pluginLog.Debug($"TickFlagArrivalAndUnmount soft-fail: {ex.Message}");
			return false;
		}
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
		// RSR stop: RsrStopTrigger.Dispose → ImmediateClear.
		instanceChange.Clear();
		mount.Clear();
		activeHuntFlag = null;
		flagArrival.Clear();
		unmount.ClearAll();
		engage.Clear();
		follow.Clear();
		combat.Clear();
		rsrEnable.Clear();
		train.Reset();
		chatMessageHandler.Dispose();
		huntAlertsIpc.Dispose();
		RsrIpc.Dispose();
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
