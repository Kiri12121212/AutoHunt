#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
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
	private readonly ContextMenuService contextMenu;
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

	/// <summary>In-flight same-zone vnav path-cost sample for time-aware TP.</summary>
	private PendingSameZoneTravelCost? pendingSameZoneTravelCost;

	/// <summary>
	/// Cross-world HuntAlerts hand-off (TASKS 10.5): single-slot stash while Lifestream
	/// <c>ChangeWorld</c> runs; flushed into <see cref="OnHuntFlagReceived"/> once on hunt world.
	/// A newer <see cref="HuntAlertsWorldVisitAction.RequestWorldVisit"/> replaces prior
	/// pending (newest wins); different-world replace soft-fails <c>Abort</c> before the new
	/// <c>ChangeWorld</c>. If that <c>ChangeWorld</c> fails after Abort,
	/// <see cref="HuntAlertsWorldVisitAction.DeferReplaceFailed"/> still Stores the incoming
	/// flag for its world (never silent-drop). Framework then soft-retries
	/// <c>ChangeWorld(pending)</c> each tick while not busy and not yet on that world
	/// (see <see cref="HuntAlertsPipelineIntake.ShouldRetryPendingChangeWorld"/>).
	/// <see cref="HuntAlertsWorldVisitAction.BusyMidVisit"/>
	/// while pending: same world → refresh flag only (keep pending world); different world →
	/// skip (avoids flush waiting for a world Lifestream is not visiting). Also used when
	/// same-world pending replace <c>ChangeWorld</c> fails without Abort (World set → refresh).
	/// After any <see cref="HuntAlertsWorldVisit.TryHandle"/> that called
	/// <c>ChangeWorld</c> (<see cref="HuntAlertsWorldVisitDecisionResult.AttemptedChangeWorld"/>),
	/// <see cref="skipHaPendingChangeWorldRetryThisTick"/> blocks same-tick soft-retry
	/// (including failed same-pending remap to BusyMidVisit).
	/// EnterPipeline while pending: same world → replace stash; different world →
	/// <c>Lifestream.Abort</c> then clear + enter (see <see cref="ApplyEnterPipeline"/>).
	/// Conductor chat clears this stash + Aborts Lifestream + clears
	/// <see cref="huntAlertsFlagQueue"/> and suppresses HA Drain for the remainder of the
	/// Framework tick (conductor-wins — IPC enqueued after Clear must not override adopt).
	/// Framework HA drain Accept does not Clear the queue during handle (flags enqueued
	/// later that tick must survive). Drain itself is newest-wins for the batch — see
	/// <see cref="HuntAlertsFlagQueue.Drain"/>.
	/// IPC enqueues onto <see cref="huntAlertsFlagQueue"/>; Framework drains before flush so
	/// Abort / ChangeWorld / Store / Take share one tick thread. Slot swaps remain atomic —
	/// see <see cref="HuntAlertsPendingDeferSlot"/>.
	/// </summary>
	private HuntAlertsPendingDefer? pendingHuntAlerts;

	/// <summary>
	/// HuntAlerts CallGate → Framework marshal (TASKS 10.5). Drain in
	/// <see cref="OnFrameworkUpdate"/> before pending soft-retry /
	/// <see cref="TryFlushPendingHuntAlerts"/> — batch dequeue then handle only the newest
	/// flag (avoids same-tick Accept then RequestWorldVisit → active hunt + conflicting
	/// pending). Cleared with pending on chat adopt / master-off / dispose — not on
	/// Framework HA drain Accept (see
	/// <see cref="HuntAlertsFlagQueue.ClearQueueOnFrameworkDrainAccept"/>).
	/// After chat Clear, <see cref="suppressHaDrainThisTick"/> skips Drain until the next
	/// Framework update starts (conductor-wins).
	/// </summary>
	private readonly ConcurrentQueue<HuntFlag> huntAlertsFlagQueue = new();

	/// <summary>
	/// Conductor-wins: after chat Clear, skip HA <see cref="HuntAlertsFlagQueue.Drain"/>
	/// for the rest of this Framework tick. Consumed at the start of the next update via
	/// <see cref="HuntAlertsFlagQueue.BeginFrameworkTick"/>.
	/// </summary>
	private bool suppressHaDrainThisTick;

	/// <summary>
	/// Last chat or HuntAlerts adopt for windowed cross-source dedupe (TASKS 10.7).
	/// First source wins for the same hunt within
	/// <see cref="HuntAlertsFlagDedupe.DefaultCrossSourceWindow"/>.
	/// Cleared on master-off / dispose with pending.
	/// </summary>
	private HuntFlagDedupeMemory? lastFlagIntakeMemory;

	/// <summary>
	/// After Drain/Process attempted <c>ChangeWorld</c> this tick
	/// (<see cref="HuntAlertsWorldVisitDecisionResult.AttemptedChangeWorld"/> — success
	/// or fail, including BusyMidVisit refresh after same-pending fail), skip
	/// <see cref="TryRetryPendingHuntAlertsChangeWorld"/> for the rest of this tick —
	/// Lifestream may still report not-busy, and a second ChangeWorld would duplicate.
	/// Cleared at <see cref="HuntAlertsFlagQueue.BeginFrameworkTick"/>.
	/// </summary>
	private bool skipHaPendingChangeWorldRetryThisTick;

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
		INotificationManager notificationManager,
		IContextMenu contextMenuService)
	{
		this.pluginInterface = pluginInterface;
		this.clientState = clientState;
		this.objectTable = objectTable;
		this.dataManager = dataManager;
		this.framework = framework;
		this.condition = condition;
		this.pluginLog = pluginLog;
		Config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
		var migratedSkipDistance = false;
		if (ConfigTabs.NeedsYalmSkipDistanceMigration(Config.Version))
		{
			Config.AutoTeleportAetheryteDistanceDiff =
				ConfigTabs.ScaleLegacyMapSkipDistanceToYalms(Config.AutoTeleportAetheryteDistanceDiff);
			Config.Version = ConfigTabs.YalmSkipDistanceConfigVersion;
			migratedSkipDistance = true;
		}
		else
		{
			Config.AutoTeleportAetheryteDistanceDiff =
				ConfigTabs.ClampAutoTeleportSkipDistance(Config.AutoTeleportAetheryteDistanceDiff);
		}

		Config.EngageRange = CombatDecision.ClampEngageRange(Config.EngageRange);
		Config.ARankScanRange = EngageTargetDecision.ClampARankScanRange(Config.ARankScanRange);
		Config.RsrHostileType = RsrSettingsDecision.ClampHostileType(Config.RsrHostileType);
		Config.RsrTargetingTank = RsrSettingsDecision.ClampTargetingType(
			Config.RsrTargetingTank,
			RsrSettingsDecision.DefaultTankTargeting);
		Config.RsrTargetingNonTank = RsrSettingsDecision.ClampTargetingType(
			Config.RsrTargetingNonTank,
			RsrSettingsDecision.DefaultNonTankTargeting);
		if (migratedSkipDistance)
			pluginInterface.SavePluginConfig(Config);

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

		// MapManager before HuntAlerts IPC so train messages resolve SizeFactor/offsets.
		mapManager = new MapManager(dataManager, msg => pluginLog.Warning(msg));
		// HuntAlerts → HuntFlag → world-visit / TP-nav intake (TASKS 10.3–10.5).
		huntAlertsIpc = new HuntAlertsIpc(
			pluginInterface,
			Config,
			territoryTypeId => mapManager.GetMapParams(mapId: 0, territoryTypeId),
			OnHuntAlertsFlag,
			ResolveTerritoryExVersion,
			pluginLog,
			() => pluginInterface.SavePluginConfig(Config));

		configWindow = new ConfigWindow(
			Config,
			() => pluginInterface.SavePluginConfig(Config),
			() => TeleporterIpc.IsAvailable,
			() => LifestreamIpc.IsAvailable,
			() => VNavmeshIpc.IsAvailable,
			() => RsrIpc.IsAvailable,
			() => huntAlertsIpc.PluginStatus,
			() => huntAlertsIpc.LastMappedAlert,
			CaptureStatus,
			debugLog,
			() => huntAlertsIpc.LastIntakeStatus);
		windowSystem.AddWindow(configWindow);

		chat = new GameChat();
		chat2Ipc = new Chat2Ipc(
			pluginInterface,
			Config,
			() => pluginInterface.SavePluginConfig(Config),
			() => configWindow.IsOpen = true);
		contextMenu = new ContextMenuService(
			contextMenuService,
			dataManager,
			pluginLog,
			Config,
			() => pluginInterface.SavePluginConfig(Config),
			() => configWindow.IsOpen = true);
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
		pluginInterface.UiBuilder.OpenMainUi += ToggleUi;
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
	/// HuntAlerts mapped-flag IPC hook (TASKS 10.4–10.5): enqueue for Framework drain
	/// so pending-slot mutations share the update tick with flush (no IPC/Update race).
	/// </summary>
	private void OnHuntAlertsFlag(HuntFlag flag)
		=> HuntAlertsFlagQueue.Enqueue(huntAlertsFlagQueue, flag);

	/// <summary>
	/// HuntAlerts mapped-flag processing on Framework tick: cross-world → Lifestream
	/// <c>ChangeWorld</c> (defer TP); same-world / unknown-current (no pending) →
	/// <see cref="ApplyEnterPipeline"/>. Unknown + occupied pending refreshes/skips
	/// like BusyMidVisit (never Abort while current world is unreadable).
	/// ApplyEnterPipeline respects pending visit: same world keeps defer,
	/// different world aborts Lifestream then enters.
	/// </summary>
	private void ProcessHuntAlertsFlag(HuntFlag flag)
	{
		try
		{
			// Chat already won this hunt: do not ChangeWorld / Store pending — a later
			// forceAccept flush must not restart (cross-source window still applies).
			if (HuntAlertsFlagDedupe.ShouldSuppressCrossSource(
				    lastFlagIntakeMemory,
				    flag,
				    HuntFlagIntakeSource.HuntAlerts,
				    DateTimeOffset.UtcNow,
				    Config.HuntAlertsIntegration))
			{
				// Clear only a stale same-hunt defer (near chat-won memory); keep an
				// unrelated pending world visit even if its coords happen to lie near
				// the suppressed incoming HA flag.
				var pendingForSuppress = Volatile.Read(ref pendingHuntAlerts);
				if (HuntAlertsFlagDedupe.ShouldClearPendingOnCrossSourceSuppress(
					    pendingForSuppress?.Flag,
					    lastFlagIntakeMemory?.Flag))
				{
					pluginLog.Information(
						"HuntAlerts defer/intake skipped (cross-source chat↔HA window dedupe); pending cleared");
					ClearPendingHuntAlerts();
				}
				else
				{
					pluginLog.Information(
						"HuntAlerts defer/intake skipped (cross-source chat↔HA window dedupe); unrelated pending kept");
				}

				return;
			}

			var pending = Volatile.Read(ref pendingHuntAlerts);
			var decision = HuntAlertsWorldVisit.TryHandle(
				flag,
				Config.HuntAlertsIntegration,
				LifestreamIpc,
				TryGetCurrentWorldName(),
				hasPendingDefer: pending != null,
				pendingDeferWorld: pending?.World);

			// Any ChangeWorld attempt this Process (success or fail) — block same-tick
			// pending soft-retry (BusyMidVisit refresh after same-pending fail included).
			if (decision.AttemptedChangeWorld)
				skipHaPendingChangeWorldRetryThisTick = true;

			if (decision.Action == HuntAlertsWorldVisitAction.RequestWorldVisit)
				pluginLog.Information($"HuntAlerts cross-world visit: {decision.World}");
			else if (decision.Action == HuntAlertsWorldVisitAction.CannotVisit)
				pluginLog.Information($"HuntAlerts cannot visit world: {decision.World}");
			else if (decision.Action == HuntAlertsWorldVisitAction.DeferReplaceFailed)
			{
				// Abort ran; ChangeWorld (+ not-busy retry) failed — do not drop incoming.
				// Intake → DeferUntilOnWorld: soft-clear prior via Store of new world.
				pluginLog.Information(
					$"HuntAlerts defer replace: ChangeWorld failed after Abort → retain pending for {decision.World}");
			}

			switch (HuntAlertsPipelineIntake.Decide(
				        decision.Action,
				        hasPendingDefer: pending != null,
				        pendingWorld: pending?.World,
				        newHuntWorld: decision.World))
			{
				case HuntAlertsPipelineIntakeKind.EnterPipeline:
					ApplyEnterPipeline(flag, decision.World);
					break;
				case HuntAlertsPipelineIntakeKind.DeferUntilOnWorld:
					// Do not start same-world TP until on the hunt world.
					// RequestWorldVisit: single slot, newer defer replaces prior (newest wins).
					// Store after successful ChangeWorld (RequestWorldVisit), or after
					// DeferReplaceFailed (Abort + failed ChangeWorld — retain incoming).
					// Different-world replace: Abort already ran in TryHandle before ChangeWorld.
					// BusyMidVisit / UnknownCurrentWorld same-world: refresh flag only —
					// pending world stays the in-flight Lifestream destination
					// (ChangeWorld was not re-issued; unknown must not AbortVisitThenEnter).
					if (decision.Action is HuntAlertsWorldVisitAction.BusyMidVisit
					    or HuntAlertsWorldVisitAction.UnknownCurrentWorld)
					{
						if (HuntAlertsPipelineIntake.DeferredStashReplacesPrior(pending != null))
							pluginLog.Information(
								$"HuntAlerts {decision.Action} → refresh pending flag (keep world {pending?.World})");
						HuntAlertsPendingDeferSlot.RefreshFlagKeepWorld(ref pendingHuntAlerts, flag);
						break;
					}

					if (string.IsNullOrEmpty(decision.World))
						break;

					if (HuntAlertsPipelineIntake.DeferredStashReplacesPrior(pending != null))
						pluginLog.Information(
							$"HuntAlerts defer replace (newest wins): discarding prior pending for {pending?.World}");
					HuntAlertsPendingDeferSlot.Store(ref pendingHuntAlerts, flag, decision.World);
					break;
				default:
					break;
			}
		}
		catch (Exception ex)
		{
			pluginLog.Debug($"ProcessHuntAlertsFlag soft-fail: {ex.Message}");
		}
	}

	/// <summary>
	/// EnterPipeline with optional pending cross-world defer (TASKS 10.5).
	/// Same pending world → replace stash, stay deferred. Different world → soft-fail
	/// <c>Lifestream.Abort</c>, clear pending, then accept so train/world stay aligned.
	/// Accept uses <c>clearPendingDefer: false</c> (HA Framework drain — do not Clear the
	/// IPC queue mid-batch; pending already cleared here when entering).
	/// </summary>
	private void ApplyEnterPipeline(HuntFlag flag, string? newHuntWorld)
	{
		var pending = Volatile.Read(ref pendingHuntAlerts);
		var disposition = HuntAlertsPipelineIntake.DecideEnterWithPending(
			pending != null,
			pending?.World,
			newHuntWorld);

		switch (disposition)
		{
			case HuntAlertsEnterWithPendingKind.ReplacePendingKeepDefer:
				var keepWorld = newHuntWorld ?? pending?.World;
				if (string.IsNullOrEmpty(keepWorld))
				{
					ClearPendingHuntAlerts();
					AcceptHuntAlertsFlag(
						flag,
						clearPendingDefer: HuntAlertsFlagQueue.ClearQueueOnFrameworkDrainAccept);
					return;
				}

				if (HuntAlertsPipelineIntake.DeferredStashReplacesPrior(pending != null))
					pluginLog.Information(
						$"HuntAlerts EnterPipeline → keep defer (same pending world): replacing pending for {pending?.World}");
				HuntAlertsPendingDeferSlot.Store(ref pendingHuntAlerts, flag, keepWorld);
				return;
			case HuntAlertsEnterWithPendingKind.AbortVisitThenEnter:
				// Near-dup / cross-source suppress check *before* Abort/clear: if Accept
				// would no-op, keep the pending visit instead of aborting for nothing.
				var pipelineActive = FlagRestartDecision.IsPipelineActive(
					train.Phase,
					HasInFlightPipelineWork());
				if (!HuntAlertsFlagDedupe.ShouldProceedAbortVisitThenEnter(
					    activeHuntFlag,
					    flag,
					    pipelineActive,
					    lastFlagIntakeMemory,
					    DateTimeOffset.UtcNow,
					    Config.HuntAlertsIntegration))
				{
					pluginLog.Information(
						"HuntAlerts AbortVisitThenEnter skipped (near-dup / cross-source suppress); pending visit kept");
					return;
				}

				try
				{
					LifestreamIpc.Abort();
				}
				catch (Exception ex)
				{
					pluginLog.Debug($"HuntAlerts Lifestream.Abort soft-fail: {ex.Message}");
				}

				pluginLog.Information(
					$"HuntAlerts abort pending Lifestream visit ({pending?.World}) before EnterPipeline");
				ClearPendingHuntAlerts();
				AcceptHuntAlertsFlag(
					flag,
					clearPendingDefer: HuntAlertsFlagQueue.ClearQueueOnFrameworkDrainAccept);
				return;
			default:
				ClearPendingHuntAlerts();
				AcceptHuntAlertsFlag(
					flag,
					clearPendingDefer: HuntAlertsFlagQueue.ClearQueueOnFrameworkDrainAccept);
				return;
		}
	}

	/// <summary>
	/// Strip untrusted IPC Arrival, recompute nearest aetheryte like chat
	/// (<see cref="TeleportDecision.Evaluate"/>), then shared restart intake.
	/// Pass <paramref name="forceAccept"/> for deferred flush after world visit so
	/// near-dup vs <see cref="activeHuntFlag"/> cannot skip Arrival trust + recompute.
	/// Cross-source window still applies (forceAccept does not bypass it).
	/// Pass <paramref name="clearPendingDefer"/> false for Framework HA drain Accept and
	/// flush (already took the slot) so mid-batch IPC flags / a newer defer survive —
	/// <see cref="AdoptHuntFlag"/> must not Clear <see cref="huntAlertsFlagQueue"/> then.
	/// When true (chat only via <see cref="OnHuntFlagReceived"/>), also clears the queue.
	/// </summary>
	private void AcceptHuntAlertsFlag(
		HuntFlag flag,
		bool forceAccept = false,
		bool clearPendingDefer = true)
	{
		var pipelineActive = FlagRestartDecision.IsPipelineActive(
			train.Phase,
			HasInFlightPipelineWork());
		var now = DateTimeOffset.UtcNow;

		if (HuntAlertsFlagDedupe.ShouldSuppress(
			    activeHuntFlag,
			    flag,
			    pipelineActive,
			    forceAccept: forceAccept))
			return;

		if (HuntAlertsFlagDedupe.ShouldSuppressCrossSource(
			    lastFlagIntakeMemory,
			    flag,
			    HuntFlagIntakeSource.HuntAlerts,
			    now,
			    Config.HuntAlertsIntegration))
		{
			pluginLog.Information(
				"HuntAlerts intake skipped (cross-source chat↔HA window dedupe)");
			return;
		}

		var instanceHint = HuntAlertsArrivalTrust.ClearUntrustedArrival(flag);
		PrepareTeleportIntent(flag, instanceHint);
		AdoptHuntFlag(flag, clearPendingDefer);
		lastFlagIntakeMemory = HuntAlertsFlagDedupe.Remember(
			flag,
			HuntFlagIntakeSource.HuntAlerts,
			now);
	}

	/// <summary>
	/// Chat-path teleport decision into <see cref="ChatMessageHandler.TeleportIntent"/>.
	/// Soft-fails like <c>ChatMessageHandler.TryEvaluateTeleportDecision</c>.
	/// </summary>
	private void PrepareTeleportIntent(HuntFlag flag, int targetInstanceHint = 0)
	{
		try
		{
			var snapshot = TryGetPlayerSnapshot(flag);
			if (snapshot is { } s && targetInstanceHint > 0)
			{
				snapshot = s with { TargetInstance = targetInstanceHint };
				if (pendingSameZoneTravelCost is { } pending && ReferenceEquals(pending.Flag, flag))
					pending.Snapshot = snapshot.Value;
			}

			var decision = TeleportDecision.Evaluate(
				Config.Enabled,
				Config.AutoTeleport,
				Config.AutoTeleportAetheryteDistanceDiff,
				Config.AutoSwitchInstanceToOne,
				flag,
				snapshot,
				ChatMessageHandler.CreateTimeAwareSettings(Config));
			chatMessageHandler.TeleportIntent.Set(decision);
			if (decision.SkipReason != TeleportSkipReason.AwaitingTravelCost)
				pendingSameZoneTravelCost = null;
		}
		catch
		{
			pendingSameZoneTravelCost = null;
			chatMessageHandler.TeleportIntent.Set(new TeleportDecisionResult
			{
				Action = TeleportAction.Skip,
				SkipReason = TeleportSkipReason.PlayerStateUnavailable,
			});
		}
	}

	/// <summary>
	/// Soft-retry <c>ChangeWorld</c> for a Stored pending when Lifestream is idle and the
	/// player is not yet on that world (e.g. after <c>DeferReplaceFailed</c>). Soft-fails.
	/// </summary>
	private void TryRetryPendingHuntAlertsChangeWorld()
	{
		try
		{
			var pending = Volatile.Read(ref pendingHuntAlerts);
			if (pending == null)
				return;

			var busy = false;
			try
			{
				busy = LifestreamIpc.IsBusy();
			}
			catch
			{
				busy = false;
			}

			if (!HuntAlertsPipelineIntake.ShouldRetryPendingChangeWorld(
				    hasPendingDefer: true,
				    pendingWorld: pending.World,
				    currentWorldName: TryGetCurrentWorldName(),
				    lifestreamBusy: busy,
				    visitJustQueuedThisTick: skipHaPendingChangeWorldRetryThisTick))
				return;

			_ = LifestreamIpc.ChangeWorld(pending.World);
		}
		catch (Exception ex)
		{
			pluginLog.Debug($"TryRetryPendingHuntAlertsChangeWorld soft-fail: {ex.Message}");
		}
	}

	private void TryFlushPendingHuntAlerts()
	{
		HuntAlertsPendingDefer? taken = null;
		var accepted = false;
		try
		{
			// Atomic take: newer Store between read and clear is left in the slot.
			taken = HuntAlertsPendingDeferSlot.TryTakeForFlush(
				ref pendingHuntAlerts,
				TryGetCurrentWorldName());
			if (taken == null)
				return;

			// Force-accept: bypass near-dup pipeline suppress so Arrival trust still strips.
			// Cross-source window still applies (chat-won hunts must not restart).
			// clearPendingDefer: false — slot already taken; do not orphan a newer defer.
			AcceptHuntAlertsFlag(taken.Flag, forceAccept: true, clearPendingDefer: false);
			accepted = true;
		}
		catch (Exception ex)
		{
			pluginLog.Debug($"TryFlushPendingHuntAlerts soft-fail: {ex.Message}");
		}
		finally
		{
			// Take cleared the slot before Accept; restore on failure so the flag is not lost.
			if (taken != null && !accepted)
				HuntAlertsPendingDeferSlot.TryRestoreIfEmpty(ref pendingHuntAlerts, taken);
		}
	}

	private void ClearPendingHuntAlerts()
		=> HuntAlertsPendingDeferSlot.Clear(ref pendingHuntAlerts);

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
		var now = DateTimeOffset.UtcNow;
		if (HuntAlertsFlagDedupe.ShouldSuppressCrossSource(
			    lastFlagIntakeMemory,
			    flag,
			    HuntFlagIntakeSource.Chat,
			    now,
			    Config.HuntAlertsIntegration))
		{
			pluginLog.Information(
				"Conductor flag skipped (cross-source chat↔HA window dedupe)");
			return;
		}

		AdoptHuntFlag(flag, clearPendingDefer: true);
		lastFlagIntakeMemory = HuntAlertsFlagDedupe.Remember(
			flag,
			HuntFlagIntakeSource.Chat,
			now);
	}

	/// <summary>
	/// Shared adopt path for chat + HuntAlerts. Flush and Framework HA drain Accept pass
	/// <paramref name="clearPendingDefer"/> false (no mid-batch queue Clear; flush already
	/// took the slot; do not Abort a visit just queued for pending —
	/// <c>AbortVisitThenEnter</c> Aborts on its own path). When true (chat): Abort + empties
	/// pending + <see cref="huntAlertsFlagQueue"/> and suppresses HA Drain for the rest of
	/// this Framework tick so IPC enqueued after Clear cannot override (conductor-wins).
	/// </summary>
	private void AdoptHuntFlag(HuntFlag flag, bool clearPendingDefer)
	{
		// Conductor chat wins concurrent HuntAlerts (TASKS 10.5): clear pending stash +
		// IPC queue + soft-fail Abort any in-flight Lifestream visit so a later
		// flush / queued ChangeWorld+Store cannot override this adopt (chat Remember
		// also arms cross-source suppress for forceAccept flush).
		// Framework HA drain + flush pass clearPendingDefer: false — never Clear / Abort
		// (would cancel a visit just queued for pending, or drop later flags and break
		// newest-wins); flush already took the slot so a newer defer is preserved.
		if (HuntAlertsFlagQueue.ShouldAbortLifestreamOnAdopt(clearPendingDefer))
		{
			try
			{
				LifestreamIpc.Abort();
			}
			catch (Exception ex)
			{
				pluginLog.Debug($"OnHuntFlagReceived Lifestream.Abort soft-fail: {ex.Message}");
			}
		}

		if (clearPendingDefer)
		{
			ClearPendingHuntAlerts();
			HuntAlertsFlagQueue.Clear(huntAlertsFlagQueue);
			// Conductor-wins: Drain later this tick must not process post-Clear IPC.
			suppressHaDrainThisTick = HuntAlertsFlagQueue.SuppressDrainAfterChatClear;
		}

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

			movement.ResetMeshPathfindRetry();
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
			var plan = TerritoryCleanupDecision.Decide(
				teleportPlan.HasActive,
				hunting,
				hasActiveHuntFlag: activeHuntFlag != null);
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

		if (plan.InvalidateFlagWorldPos && activeHuntFlag != null)
			activeHuntFlag.WorldPos = null;

		// Fresh soft-retry budget after mesh reload / zone swap.
		if (plan.StopNavPath || plan.InvalidateFlagWorldPos)
			movement.ResetMeshPathfindRetry();

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
		TryFinalizePendingSameZoneTravelCost(now);
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
			ClearPendingHuntAlerts();
			lastFlagIntakeMemory = HuntAlertsFlagDedupe.Clear(lastFlagIntakeMemory);
			HuntAlertsFlagQueue.Clear(huntAlertsFlagQueue);
			mount.Clear();
			unmount.ClearAll();
			engage.Clear();
			combat.Clear();
			rsrEnable.Clear();
			train.Reset();
			ObserveDebugSignals();
			return;
		}

		// Cross-world HuntAlerts: drain IPC queue, soft-retry pending ChangeWorld if needed,
		// then enter TP/nav once on hunt world. Newest-wins drain before flush so
		// Abort/ChangeWorld/Store and Take share this tick. After chat Clear, skip Drain
		// for the remainder of that tick (conductor-wins); consume suppress here.
		// Begin also clears skipHaPendingChangeWorldRetryThisTick (set when Process
		// successfully queued ChangeWorld — soft-retry must not duplicate same tick).
		if (HuntAlertsFlagQueue.BeginFrameworkTick(
			    ref suppressHaDrainThisTick,
			    ref skipHaPendingChangeWorldRetryThisTick))
			HuntAlertsFlagQueue.Drain(huntAlertsFlagQueue, ProcessHuntAlertsFlag);
		TryRetryPendingHuntAlertsChangeWorld();
		TryFlushPendingHuntAlerts();

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
			{
				pendingSameZoneTravelCost = null;
				return null;
			}

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
			var sameZone = clientState.TerritoryType == flag.TerritoryTypeId;
			if (sameZone)
			{
				var pos = player.Position;
				var flagXz = FlagWorldPosition.WorldXZFromRaw(flag.RawX, flag.RawY);
				distance = MapCoordinates.WorldXZDistance(pos.X, pos.Z, flagXz.X, flagXz.Y);
			}

			var travelEstimate = TrySampleSameZoneTravelEstimate(
				flag,
				player.Position,
				sameZone,
				distance,
				nearest,
				mapParams);

			var lifestreamInstance = LifestreamIpc.GetCurrentInstance();
			var snapshot = new TeleportPlayerSnapshot
			{
				CurrentTerritory = clientState.TerritoryType,
				CurrentInstance = lifestreamInstance > 0 ? lifestreamInstance : (int)clientState.Instance,
				TargetInstance = 0,
				PlayerDistance = distance,
				Nearest = nearest,
				TravelEstimate = travelEstimate,
			};

			if (pendingSameZoneTravelCost is { } pending && ReferenceEquals(pending.Flag, flag))
				pending.Snapshot = snapshot;

			return snapshot;
		}
		catch (Exception ex)
		{
			pendingSameZoneTravelCost = null;
			pluginLog.Debug($"TryGetPlayerSnapshot soft-fail: {ex.Message}");
			return null;
		}
	}

	/// <summary>
	/// Same-zone only: start/resolve vnav path lengths for time-aware TP.
	/// Soft-fails to null / Unavailable (distance threshold) when vnav or endpoints missing.
	/// </summary>
	private SameZoneTravelEstimate? TrySampleSameZoneTravelEstimate(
		HuntFlag flag,
		Vector3 playerPos,
		bool sameZone,
		float? distanceYalms,
		NearestAetheryteResult? nearest,
		MapCoordParams? mapParams)
	{
		pendingSameZoneTravelCost = null;
		if (!Config.AutoTeleportTimeAware || !sameZone)
			return null;

		if (Config.AutoTeleportRetainDistanceFloor
			&& distanceYalms is { } d
			&& d <= Config.AutoTeleportAetheryteDistanceDiff)
			return null;

		if (nearest == null || mapParams == null)
			return SameZonePathCostSampler.Unavailable();

		try
		{
			// Zero is "unset" elsewhere (nav / arrival); do not pathfind to origin.
			Vector3? flagFloor = flag.WorldPos is { } wp && wp != Vector3.Zero
				? wp
				: flagWorld.TryResolve(flag);
			if (flagFloor is null || flagFloor == Vector3.Zero)
				return SameZonePathCostSampler.Unavailable();

			var p = mapParams.Value;
			var aethWorldX = MapCoordinates.ConvertMapCoordinateToWorld(nearest.Value.MapX, p.SizeFactor, p.OffsetX);
			var aethWorldZ = MapCoordinates.ConvertMapCoordinateToWorld(nearest.Value.MapY, p.SizeFactor, p.OffsetY);
			var aethFloor = VNavmeshIpc.QueryMeshPointOnFloor(
				FlagWorldPosition.PointOnFloorQueryFromWorldXZ(aethWorldX, aethWorldZ),
				FlagWorldPosition.DefaultAllowUnlandable,
				FlagWorldPosition.DefaultHalfExtentXZ);
			if (aethFloor == null)
				return SameZonePathCostSampler.Unavailable();

			if (!SameZonePathCostSampler.TryBegin(
				    VNavmeshIpc,
				    playerPos,
				    flagFloor.Value,
				    aethFloor.Value,
				    canFly: false,
				    out var estimate,
				    out var directTask,
				    out var aethTask))
				return SameZonePathCostSampler.Unavailable();

			if (estimate.Status == SameZonePathCostStatus.Pending
				&& directTask != null
				&& aethTask != null)
			{
				pendingSameZoneTravelCost = new PendingSameZoneTravelCost
				{
					Flag = flag,
					Snapshot = default,
					Direct = directTask,
					FromAetheryte = aethTask,
					StartedAtMs = Environment.TickCount64,
				};
			}

			return estimate;
		}
		catch (Exception ex)
		{
			pluginLog.Debug($"TrySampleSameZoneTravelEstimate soft-fail: {ex.Message}");
			return SameZonePathCostSampler.Unavailable();
		}
	}

	private void TryFinalizePendingSameZoneTravelCost(long nowMs)
	{
		var pending = pendingSameZoneTravelCost;
		if (pending == null)
			return;

		try
		{
			if (chatMessageHandler.TeleportIntent.LatestDecision is not
			    { Action: TeleportAction.Skip, SkipReason: TeleportSkipReason.AwaitingTravelCost })
			{
				pendingSameZoneTravelCost = null;
				return;
			}

			SameZoneTravelEstimate estimate;
			if (nowMs - pending.StartedAtMs >= SameZonePathCostSampler.DefaultTimeoutMs)
			{
				estimate = SameZonePathCostSampler.Unavailable();
			}
			else if (!SameZonePathCostSampler.TryResolve(pending.Direct, pending.FromAetheryte, out estimate))
			{
				return;
			}

			pendingSameZoneTravelCost = null;

			var snapshot = pending.Snapshot with { TravelEstimate = estimate };
			if (snapshot.TargetInstance == 0 && pending.Flag.Arrival is { Instance: > 0 } arr)
				snapshot = snapshot with { TargetInstance = arr.Instance };

			var decision = TeleportDecision.Evaluate(
				Config.Enabled,
				Config.AutoTeleport,
				Config.AutoTeleportAetheryteDistanceDiff,
				Config.AutoSwitchInstanceToOne,
				pending.Flag,
				snapshot,
				ChatMessageHandler.CreateTimeAwareSettings(Config));
			chatMessageHandler.TeleportIntent.Set(decision);
			TryEngageAfterTravelCost(decision);
		}
		catch (Exception ex)
		{
			pendingSameZoneTravelCost = null;
			pluginLog.Debug($"TryFinalizePendingSameZoneTravelCost soft-fail: {ex.Message}");
		}
	}

	/// <summary>
	/// After deferred time-aware resolve: adopt TP or mount/nav if still Idle and no plan.
	/// </summary>
	private void TryEngageAfterTravelCost(TeleportDecisionResult decision)
	{
		if (teleportPlan.HasActive)
			return;

		var adopted = teleportPlan.TryAdoptFromIntent(chatMessageHandler.TeleportIntent);
		var alreadyClose = decision is
			{ Action: TeleportAction.Skip, SkipReason: TeleportSkipReason.AlreadyClose };

		if (adopted)
		{
			ApplyDelayTeleport();
			pluginLog.Information("Engaging autoteleport (time-aware)");
			if (train.Phase == HuntTrainPhase.Idle)
				train.Apply(HuntTrainEvent.StartTeleport);
			return;
		}

		if (!alreadyClose)
			return;

		mount.EnqueueIfEnabled(Config.UseMount);
		if (train.Phase != HuntTrainPhase.Idle)
			return;

		var start = HuntTrainObserve.DecideFlagStart(
			Config.Enabled,
			teleportPlanActive: false,
			alreadyCloseSkip: true,
			Config.UseMount);
		if (start != HuntTrainEvent.None)
			train.Apply(start);
	}

	private uint? GetIntendedUseRowId(uint territoryId) =>
		dataManager.GetExcelSheet<TerritoryType>()?.GetRowOrDefault(territoryId)?.TerritoryIntendedUse.RowId;

	private uint? ResolveTerritoryExVersion(uint territoryId) =>
		dataManager.GetExcelSheet<TerritoryType>()?.GetRowOrDefault(territoryId)?.ExVersion.RowId;

	private void Draw() => windowSystem.Draw();

	private void ToggleUi()
	{
		configWindow.IsOpen = !configWindow.IsOpen;
		pluginInterface.SavePluginConfig(Config);
	}

	public void Dispose()
	{
		pendingSameZoneTravelCost = null;
		framework.Update -= OnFrameworkUpdate;
		clientState.TerritoryChanged -= OnTerritoryChanged;
		chatMessageHandler.HuntFlagReceived -= OnHuntFlagReceived;
		// RSR stop: RsrStopTrigger.Dispose → ImmediateClear.
		ClearPendingHuntAlerts();
		lastFlagIntakeMemory = HuntAlertsFlagDedupe.Clear(lastFlagIntakeMemory);
		HuntAlertsFlagQueue.Clear(huntAlertsFlagQueue);
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
		contextMenu.Dispose();
		pluginInterface.UiBuilder.Draw -= Draw;
		pluginInterface.UiBuilder.OpenMainUi -= ToggleUi;
		pluginInterface.UiBuilder.OpenConfigUi -= ToggleUi;
		windowSystem.RemoveAllWindows();
		configWindow.Dispose();
	}

	/// <summary>Deferred vnav path-cost sample for same-zone time-aware TP.</summary>
	private sealed class PendingSameZoneTravelCost
	{
		public required HuntFlag Flag { get; init; }
		public TeleportPlayerSnapshot Snapshot { get; set; }
		public required Task<List<Vector3>> Direct { get; init; }
		public required Task<List<Vector3>> FromAetheryte { get; init; }
		public long StartedAtMs { get; init; }
	}
}
