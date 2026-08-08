#nullable enable
using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using HuntTrainAuto.Domain;
using HuntTrainAuto.HuntAlerts;

namespace HuntTrainAuto.Windows;

public sealed class ConfigWindow : Window, IDisposable
{
	private static readonly Vector4 AvailableColor = new(0.45f, 0.85f, 0.45f, 1f);
	private static readonly Vector4 MissingColor = new(0.95f, 0.40f, 0.40f, 1f);

	private readonly Configuration config;
	private readonly Action saveConfig;
	private readonly Func<bool> teleporterAvailable;
	private readonly Func<bool> lifestreamAvailable;
	private readonly Func<bool> vnavmeshAvailable;
	private readonly Func<bool> rsrAvailable;
	private readonly Func<bool> bossModAvailable;
	private readonly Func<HuntAlertsPluginStatus> huntAlertsStatus;
	private readonly Func<HuntAlertsLastAlert?> getHuntAlertsLastAlert;
	private readonly Func<string?> getHuntAlertsLastIntake;
	private readonly Func<HuntTrainMessage?> getHuntAlertsLastMessage;
	private readonly Action? openAlertInfo;
	private readonly Func<StatusSnapshot> getStatus;
	private readonly DebugEventLog debugLog;
	private string conductorInput = string.Empty;
	private int selectedConductor;
	private int selectedTab;

	public ConfigWindow(
		Configuration config,
		Action saveConfig,
		Func<bool> teleporterAvailable,
		Func<bool> lifestreamAvailable,
		Func<bool> vnavmeshAvailable,
		Func<bool> rsrAvailable,
		Func<bool> bossModAvailable,
		Func<HuntAlertsPluginStatus> huntAlertsStatus,
		Func<HuntAlertsLastAlert?> getHuntAlertsLastAlert,
		Func<StatusSnapshot> getStatus,
		DebugEventLog debugLog,
		Func<string?>? getHuntAlertsLastIntake = null,
		Func<HuntTrainMessage?>? getHuntAlertsLastMessage = null,
		Action? openAlertInfo = null) : base(PluginVersion.WindowTitle)
	{
		this.config = config;
		this.saveConfig = saveConfig;
		this.teleporterAvailable = teleporterAvailable;
		this.lifestreamAvailable = lifestreamAvailable;
		this.vnavmeshAvailable = vnavmeshAvailable;
		this.rsrAvailable = rsrAvailable;
		this.bossModAvailable = bossModAvailable;
		this.huntAlertsStatus = huntAlertsStatus;
		this.getHuntAlertsLastAlert = getHuntAlertsLastAlert;
		this.getHuntAlertsLastIntake = getHuntAlertsLastIntake ?? (() => null);
		this.getHuntAlertsLastMessage = getHuntAlertsLastMessage ?? (() => null);
		this.openAlertInfo = openAlertInfo;
		this.getStatus = getStatus;
		this.debugLog = debugLog;
		SizeConstraints = new WindowSizeConstraints
		{
			MinimumSize = new Vector2(420, 420),
			MaximumSize = new Vector2(900, 700),
		};
	}

	public override void Draw()
	{
		DrawMasterAndConductors();
		ImGui.Spacing();
		ImGui.Separator();
		ImGui.Spacing();

		selectedTab = ConfigTabs.ClampSelected(selectedTab);
		if (!ImGui.BeginTabBar("##htaConfigTabs"))
			return;

		if (ImGui.BeginTabItem(ConfigTabs.Labels[ConfigTabs.Status]))
		{
			selectedTab = ConfigTabs.Status;
			DrawStatusTab();
			ImGui.EndTabItem();
		}

		if (ImGui.BeginTabItem(ConfigTabs.Labels[ConfigTabs.Settings]))
		{
			selectedTab = ConfigTabs.Settings;
			DrawSettingsTab();
			ImGui.EndTabItem();
		}

		if (ImGui.BeginTabItem(ConfigTabs.Labels[ConfigTabs.Mount]))
		{
			selectedTab = ConfigTabs.Mount;
			DrawMountTab();
			ImGui.EndTabItem();
		}

		if (ImGui.BeginTabItem(ConfigTabs.Labels[ConfigTabs.Engage]))
		{
			selectedTab = ConfigTabs.Engage;
			DrawEngageTab();
			ImGui.EndTabItem();
		}

		if (ImGui.BeginTabItem(ConfigTabs.Labels[ConfigTabs.Combat]))
		{
			selectedTab = ConfigTabs.Combat;
			DrawCombatTab();
			ImGui.EndTabItem();
		}

		if (ImGui.BeginTabItem(ConfigTabs.Labels[ConfigTabs.Integrations]))
		{
			selectedTab = ConfigTabs.Integrations;
			DrawIntegrationsTab();
			ImGui.EndTabItem();
		}

		if (ImGui.BeginTabItem(ConfigTabs.Labels[ConfigTabs.Debug]))
		{
			selectedTab = ConfigTabs.Debug;
			DrawDebugTab();
			ImGui.EndTabItem();
		}

		ImGui.EndTabBar();
	}

	private void DrawMasterAndConductors()
	{
		var enabled = config.Enabled;
		if (ImGui.Checkbox("Enabled", ref enabled))
		{
			config.Enabled = enabled;
			saveConfig();
		}

		ImGui.Spacing();
		ImGui.Text($"Conductors: {config.Conductors.Count}");
		ImGui.SameLine();
		if (ImGui.SmallButton("Clear"))
		{
			ConductorList.Clear(config.Conductors);
			selectedConductor = 0;
			saveConfig();
		}

		ImGui.SameLine();
		if (ImGui.SmallButton("Remove selected"))
		{
			if (ConductorList.TryRemoveAt(config.Conductors, selectedConductor))
			{
				if (selectedConductor >= config.Conductors.Count)
					selectedConductor = Math.Max(0, config.Conductors.Count - 1);
				saveConfig();
			}
		}

		var names = config.Conductors.ToArray();
		var height = Math.Clamp(names.Length, 1, 3);
		ImGui.SetNextItemWidth(-1);
		ImGui.ListBox("##conds", ref selectedConductor, names, height);

		ImGui.Text("Add conductor:");
		ImGui.SameLine();
		ImGui.SetNextItemWidth(150f);
		if (ImGui.InputText("##newCond", ref conductorInput, 50, ImGuiInputTextFlags.EnterReturnsTrue))
		{
			if (ConductorList.TryAdd(config.Conductors, conductorInput))
			{
				conductorInput = string.Empty;
				saveConfig();
			}
		}
	}

	private void DrawStatusTab()
	{
		ImGui.TextWrapped("Live train pipeline status (read-only).");
		ImGui.Spacing();
		ImGui.TextDisabled(PluginVersion.StatusLine);
		ImGui.Spacing();

		var snap = StatusDisplay.SafeCapture(getStatus);
		ImGui.Text(StatusDisplay.FormatPhaseLine(snap.Phase));
		ImGui.Text(StatusDisplay.FormatMountLine(
			snap.Mounted,
			snap.MountPipeline,
			snap.UnmountPipeline));
		ImGui.Text(StatusDisplay.FormatNavLine(
			snap.NavPathRunning,
			snap.NavWaypoints,
			snap.NavPathfindInProgress));
		ImGui.Text(StatusDisplay.FormatBossModAvailable(
			snap.BossModAvailable,
			snap.BossModProviderName));
		ImGui.Text(StatusDisplay.FormatBossModAi(snap.BossModAiActive));

		ImGui.Spacing();
		ImGui.Separator();
		ImGui.Spacing();
		ImGui.Text("HuntAlerts");
		var lastMessage = SafeGetHuntAlertsLastMessage();
		ImGui.TextWrapped(AlertInfoDisplay.FormatStatusSummary(lastMessage));
		if (lastMessage != null && openAlertInfo != null && ImGui.Button("Open alert info"))
			openAlertInfo();
	}

	private void DrawSettingsTab()
	{
		var autoTeleport = config.AutoTeleport;
		if (ImGui.Checkbox("Auto-teleport on conductor flag", ref autoTeleport))
		{
			config.AutoTeleport = autoTeleport;
			saveConfig();
		}

		var autoOpenMap = config.AutoOpenMap;
		if (ImGui.Checkbox("Auto-open map on conductor flag", ref autoOpenMap))
		{
			config.AutoOpenMap = autoOpenMap;
			saveConfig();
		}

		var suppressChat = config.SuppressChatOtherPlayers;
		if (ImGui.Checkbox("Suppress chat from other players", ref suppressChat))
		{
			config.SuppressChatOtherPlayers = suppressChat;
			saveConfig();
		}

		var contextMenu = config.ContextMenu;
		if (ImGui.Checkbox("Context menu (Add as conductor)", ref contextMenu))
		{
			config.ContextMenu = contextMenu;
			saveConfig();
		}

		var noDuplicateFlags = config.NoDuplicateFlags;
		if (ImGui.Checkbox("Skip duplicate flags (same zone, ≤10)", ref noDuplicateFlags))
		{
			config.NoDuplicateFlags = noDuplicateFlags;
			saveConfig();
		}

		var autoSwitchInstance = config.AutoSwitchInstanceToOne;
		if (ImGui.Checkbox("Auto-switch instance to 1 after TP", ref autoSwitchInstance))
		{
			config.AutoSwitchInstanceToOne = autoSwitchInstance;
			saveConfig();
		}

		var distanceHack = config.DistanceCompensationHack;
		if (ImGui.Checkbox("Distance compensation hack", ref distanceHack))
		{
			config.DistanceCompensationHack = distanceHack;
			saveConfig();
		}

		ImGui.Spacing();
		var skipDist = config.AutoTeleportAetheryteDistanceDiff;
		ImGui.SetNextItemWidth(200f);
		if (ImGui.SliderFloat(
			    "Same-zone TP skip distance (yalms)",
			    ref skipDist,
			    ConfigTabs.MinAutoTeleportSkipDistance,
			    ConfigTabs.MaxAutoTeleportSkipDistance,
			    "%.1f"))
		{
			config.AutoTeleportAetheryteDistanceDiff = ConfigTabs.ClampAutoTeleportSkipDistance(skipDist);
			saveConfig();
		}

		var timeAware = config.AutoTeleportTimeAware;
		if (ImGui.Checkbox("Time-aware same-zone TP (vnav path cost)", ref timeAware))
		{
			config.AutoTeleportTimeAware = timeAware;
			saveConfig();
		}

		var retainFloor = config.AutoTeleportRetainDistanceFloor;
		if (ImGui.Checkbox("Keep distance threshold as floor / fallback", ref retainFloor))
		{
			config.AutoTeleportRetainDistanceFloor = retainFloor;
			saveConfig();
		}

		ImGui.SetNextItemWidth(200f);
		var castSec = config.AutoTeleportCastSeconds;
		if (ImGui.SliderFloat(
			    "TP cast estimate (s)",
			    ref castSec,
			    ConfigTabs.MinTeleportCastSeconds,
			    ConfigTabs.MaxTeleportCastSeconds,
			    "%.1f"))
		{
			config.AutoTeleportCastSeconds = ConfigTabs.ClampTeleportCastSeconds(castSec);
			saveConfig();
		}

		ImGui.SetNextItemWidth(200f);
		var loadSec = config.AutoTeleportLoadEstimateSeconds;
		if (ImGui.SliderFloat(
			    "TP load estimate (s)",
			    ref loadSec,
			    ConfigTabs.MinTeleportLoadEstimateSeconds,
			    ConfigTabs.MaxTeleportLoadEstimateSeconds,
			    "%.1f"))
		{
			config.AutoTeleportLoadEstimateSeconds = ConfigTabs.ClampTeleportLoadEstimateSeconds(loadSec);
			saveConfig();
		}

		ImGui.SetNextItemWidth(200f);
		var mountSpeed = config.AutoTeleportMountSpeedYalmsPerSec;
		if (ImGui.SliderFloat(
			    "Mount speed (yalms/s)",
			    ref mountSpeed,
			    ConfigTabs.MinMountSpeedYalmsPerSec,
			    ConfigTabs.MaxMountSpeedYalmsPerSec,
			    "%.1f"))
		{
			config.AutoTeleportMountSpeedYalmsPerSec = ConfigTabs.ClampMountSpeedYalmsPerSec(mountSpeed);
			saveConfig();
		}

		ImGui.SetNextItemWidth(200f);
		var mountUp = config.AutoTeleportMountUpSeconds;
		if (ImGui.SliderFloat(
			    "Mount-up overhead (s)",
			    ref mountUp,
			    ConfigTabs.MinMountUpSeconds,
			    ConfigTabs.MaxMountUpSeconds,
			    "%.1f"))
		{
			config.AutoTeleportMountUpSeconds = ConfigTabs.ClampMountUpSeconds(mountUp);
			saveConfig();
		}

		ImGui.Spacing();
		ImGui.Text("Teleport delay");
		var delayEnabled = config.TeleportDelayEnabled;
		if (ImGui.Checkbox("Random pre-delay before TP", ref delayEnabled))
		{
			config.TeleportDelayEnabled = delayEnabled;
			saveConfig();
		}

		var delayMin = config.TeleportDelayMin;
		ImGui.SetNextItemWidth(200f);
		if (ImGui.SliderInt("TP delay min (ms)", ref delayMin, ConfigTabs.MinTeleportDelayMs, ConfigTabs.MaxTeleportDelayMs))
		{
			var (min, max) = ConfigTabs.ClampTeleportDelayRange(delayMin, config.TeleportDelayMax);
			config.TeleportDelayMin = min;
			config.TeleportDelayMax = max;
			saveConfig();
		}

		var delayMax = config.TeleportDelayMax;
		ImGui.SetNextItemWidth(200f);
		if (ImGui.SliderInt("TP delay max (ms)", ref delayMax, ConfigTabs.MinTeleportDelayMs, ConfigTabs.MaxTeleportDelayMs))
		{
			var (min, max) = ConfigTabs.ClampTeleportDelayRange(config.TeleportDelayMin, delayMax);
			config.TeleportDelayMin = min;
			config.TeleportDelayMax = max;
			saveConfig();
		}

		ImGui.Spacing();
		ImGui.Text("Notifications");
		var enableNotifications = config.EnableNotifications;
		if (ImGui.Checkbox("Notify on conductor flag", ref enableNotifications))
		{
			config.EnableNotifications = enableNotifications;
			saveConfig();
		}

		var enableSound = config.EnableNotificationSound;
		if (ImGui.Checkbox("Play sound on conductor flag", ref enableSound))
		{
			config.EnableNotificationSound = enableSound;
			saveConfig();
		}

		var showHaInfo = config.ShowHuntAlertsInfoWindow;
		if (ImGui.Checkbox("Open alert info on HuntAlerts map", ref showHaInfo))
		{
			config.ShowHuntAlertsInfoWindow = showHaInfo;
			saveConfig();
		}

		var showHaChat = config.ShowHuntAlertsChatNotice;
		if (ImGui.Checkbox("Chat notice on HuntAlerts map (click for info)", ref showHaChat))
		{
			config.ShowHuntAlertsChatNotice = showHaChat;
			saveConfig();
		}
	}

	private void DrawMountTab()
	{
		var useMount = config.UseMount;
		if (ImGui.Checkbox("Use mount (after TP / before nav)", ref useMount))
		{
			config.UseMount = useMount;
			saveConfig();
		}

		ImGui.TextWrapped(
			"Mount id: -1 = never, 0 = random, other = specific Mount RowId.");
		var mount = config.Mount;
		ImGui.SetNextItemWidth(200f);
		if (ImGui.InputInt("Mount selection", ref mount))
			config.Mount = ConfigTabs.ClampMountId(mount);
		// Persist only when editing finishes — avoid saving partial multi-digit RowIds.
		if (ImGui.IsItemDeactivatedAfterEdit())
		{
			config.Mount = ConfigTabs.ClampMountId(config.Mount);
			saveConfig();
		}

		ImGui.TextDisabled(ConfigTabs.FormatMountSelection(config.Mount));

		var autoUnmount = config.AutoUnmountAtFlag;
		if (ImGui.Checkbox("Auto-unmount at flag", ref autoUnmount))
		{
			config.AutoUnmountAtFlag = autoUnmount;
			saveConfig();
		}

		var autoJoinPf = config.AutoJoinHuntPf;
		if (ImGui.Checkbox("Auto-join hunt Party Finder at flag", ref autoJoinPf))
		{
			config.AutoJoinHuntPf = autoJoinPf;
			saveConfig();
		}

		ImGui.BeginDisabled(!config.AutoJoinHuntPf);
		var pfRetry = config.HuntPfRetryIntervalMs;
		ImGui.SetNextItemWidth(200f);
		if (ImGui.SliderInt(
			    "Hunt PF retry interval (ms)",
			    ref pfRetry,
			    HuntPfDecision.MinRetryIntervalMs,
			    HuntPfDecision.MaxRetryIntervalMs))
		{
			config.HuntPfRetryIntervalMs = HuntPfDecision.ClampRetryIntervalMs(pfRetry);
			saveConfig();
		}

		ImGui.EndDisabled();

		var arrival = config.FlagArrivalTolerance;
		ImGui.SetNextItemWidth(200f);
		if (ImGui.SliderFloat(
			    "Flag arrival tolerance (yalms)",
			    ref arrival,
			    ConfigTabs.MinFlagArrivalTolerance,
			    ConfigTabs.MaxFlagArrivalTolerance,
			    "%.1f"))
		{
			config.FlagArrivalTolerance = ConfigTabs.ClampFlagArrivalTolerance(arrival);
			saveConfig();
		}
	}

	private void DrawEngageTab()
	{
		ImGui.TextWrapped(
			"After the mark: path to the flag, then approach the mob on foot at engage range, unmount, and fight. " +
			"Does not follow players.");
		ImGui.Spacing();

		var engageRange = config.EngageRange;
		ImGui.SetNextItemWidth(200f);
		if (ImGui.SliderFloat(
			    "Engage range (yalms)",
			    ref engageRange,
			    CombatDecision.MinEngageRange,
			    CombatDecision.MaxEngageRange,
			    "%.0f"))
		{
			config.EngageRange = CombatDecision.ClampEngageRange(engageRange);
			saveConfig();
		}

		ImGui.TextWrapped(
			$"Tanks and melee DPS stop pathing at {CombatDecision.DefaultMeleeEngageRange:0} yalms " +
			"(melee), even when this slider is higher — RSR will not walk you in.");

		var aRankScan = config.ARankScanRange;
		ImGui.SetNextItemWidth(200f);
		if (ImGui.SliderFloat(
			    "A-rank scan range (yalms)",
			    ref aRankScan,
			    EngageTargetDecision.MinARankScanRange,
			    EngageTargetDecision.MaxARankScanRange,
			    "%.0f"))
		{
			config.ARankScanRange = EngageTargetDecision.ClampARankScanRange(aRankScan);
			saveConfig();
		}
	}

	private void DrawCombatTab()
	{
		ImGui.TextWrapped(
			"RSR = GCD rotation. BossMod AI = hunt dodge / safe-zone movement. " +
			"HTA sets BM ForbidActions while enabling AI so they do not fight for casts.");
		ImGui.Spacing();
		DrawRsrHostileCombo();
		DrawRsrTargetingCombo("Tank targeting", config.RsrTargetingTank, v => config.RsrTargetingTank = v);
		DrawRsrTargetingCombo("Non-tank targeting", config.RsrTargetingNonTank, v => config.RsrTargetingNonTank = v);

		ImGui.Spacing();
		ImGui.Separator();
		ImGui.Spacing();
		var bm = config.BossModIntegration;
		if (ImGui.Checkbox("Enable BossMod AI in combat", ref bm))
		{
			config.BossModIntegration = bm;
			saveConfig();
		}

		ImGui.BeginDisabled(!config.BossModIntegration);
		DrawBossModPreferenceCombo();
		ImGui.EndDisabled();
	}

	private void DrawIntegrationsTab()
	{
		ImGui.Text("Plugin availability:");
		DrawDependencyLine(DependencyAvailability.TeleporterDisplayName, teleporterAvailable);
		DrawDependencyLine(DependencyAvailability.LifestreamDisplayName, lifestreamAvailable);
		DrawDependencyLine(DependencyAvailability.VnavmeshDisplayName, vnavmeshAvailable);
		DrawDependencyLine(DependencyAvailability.RsrDisplayName, rsrAvailable);
		DrawDependencyLine(DependencyAvailability.BossModDisplayName, bossModAvailable);
		DrawHuntAlertsAvailabilityLine();

		ImGui.Spacing();
		var huntAlerts = config.HuntAlertsIntegration;
		if (ImGui.Checkbox("Enable HuntAlerts integration", ref huntAlerts))
		{
			config.HuntAlertsIntegration = huntAlerts;
			saveConfig();
		}

		ImGui.BeginDisabled(!config.HuntAlertsIntegration);
		ImGui.Indent();
		var autoConductor = config.HuntAlertsAutoConductor;
		if (ImGui.Checkbox("Auto-assign conductor from HuntAlerts message", ref autoConductor))
		{
			config.HuntAlertsAutoConductor = autoConductor;
			saveConfig();
		}

		ImGui.TextDisabled("Parses \"Conductor - Name\" / \"Conductor: [World] Name\" from toast text.");
		ImGui.Spacing();
		ImGui.Text("Accept ranks");
		DrawHuntAlertsRankCheckbox("A-rank trains (new_hunt)", HuntMarkRank.A);
		DrawHuntAlertsRankCheckbox("S-rank alerts (srank)", HuntMarkRank.S);

		ImGui.Spacing();
		ImGui.Text("Accept expansions");
		ImGui.TextDisabled("Empty = all. Prefers start-zone ExVersion; else HuntAlerts huntKind.");
		foreach (var group in HuntAlertsFilter.TrainGroups.All)
			DrawHuntAlertsTrainGroupCheckbox(group);
		ImGui.Unindent();
		ImGui.EndDisabled();

		ImGui.TextDisabled(HuntAlertsAvailability.FormatLastAlertStatus(
			SafeGetHuntAlertsLastAlert()));
		ImGui.TextDisabled(HuntAlertsAvailability.FormatLastIntakeStatus(
			SafeGetHuntAlertsLastIntake()));
		if (openAlertInfo != null && ImGui.Button("Open HuntAlerts alert info"))
			openAlertInfo();
		ImGui.TextWrapped(
			"When a hunt mark notification is received from HuntAlerts, automatically teleport to the target world and zone.");
	}

	private void DrawHuntAlertsRankCheckbox(string label, HuntMarkRank rank)
	{
		var enabled = HuntAlertsFilter.IsRankFilterEnabled(config.HuntAlertsRankFilter, rank);
		if (!ImGui.Checkbox(label, ref enabled))
			return;

		HuntAlertsFilter.SetRankFilterEnabled(config.HuntAlertsRankFilter, rank, enabled);
		saveConfig();
	}

	private void DrawHuntAlertsTrainGroupCheckbox(string group)
	{
		var enabled = HuntAlertsFilter.IsTrainGroupFilterEnabled(
			config.HuntAlertsTrainGroupFilter,
			group);
		if (!ImGui.Checkbox(group, ref enabled))
			return;

		HuntAlertsFilter.SetTrainGroupFilterEnabled(
			config.HuntAlertsTrainGroupFilter,
			group,
			enabled);
		saveConfig();
	}

	private void DrawHuntAlertsAvailabilityLine()
	{
		var status = HuntAlertsAvailability.SafeEvaluate(huntAlertsStatus);
		var color = status == HuntAlertsPluginStatus.Available ? AvailableColor : MissingColor;
		ImGui.TextColored(color, HuntAlertsAvailability.FormatAvailabilityLine(status));
	}

	private HuntAlertsLastAlert? SafeGetHuntAlertsLastAlert()
	{
		try
		{
			return getHuntAlertsLastAlert();
		}
		catch
		{
			return null;
		}
	}

	private string? SafeGetHuntAlertsLastIntake()
	{
		try
		{
			return getHuntAlertsLastIntake();
		}
		catch
		{
			return null;
		}
	}

	private HuntTrainMessage? SafeGetHuntAlertsLastMessage()
	{
		try
		{
			return getHuntAlertsLastMessage();
		}
		catch
		{
			return null;
		}
	}

	private void DrawDebugTab()
	{
		var debugLogging = config.EnableDebugLogging;
		if (ImGui.Checkbox("Record automation events", ref debugLogging))
		{
			config.EnableDebugLogging = debugLogging;
			saveConfig();
		}

		ImGui.SameLine();
		if (ImGui.SmallButton("Clear log"))
			debugLog.Clear();

		ImGui.TextDisabled($"Recent events ({debugLog.Count}/{debugLog.Capacity}, newest first)");
		ImGui.Spacing();

		var events = debugLog.SnapshotNewestFirst();
		if (events.Count == 0)
		{
			ImGui.TextDisabled("(empty)");
			return;
		}

		var height = Math.Clamp(events.Count, 4, 16) * ImGui.GetTextLineHeightWithSpacing();
		if (!ImGui.BeginChild("##htaDebugLog", new Vector2(-1, height), border: true))
			return;

		foreach (var e in events)
			ImGui.TextUnformatted(DebugEventFormatter.FormatLine(e));

		ImGui.EndChild();
	}

	private static void DrawDependencyLine(string displayName, Func<bool> probe)
	{
		var available = DependencyAvailability.SafeIsAvailable(probe);
		var color = available ? AvailableColor : MissingColor;
		ImGui.TextColored(color, DependencyAvailability.FormatLine(displayName, available));
	}

	private void DrawRsrHostileCombo()
	{
		ImGui.SetNextItemWidth(280f);
		var current = config.RsrHostileType;
		if (!ImGui.BeginCombo("Hostile type", current.ToString()))
			return;

		foreach (RsrTargetHostileType value in Enum.GetValues<RsrTargetHostileType>())
		{
			var selected = value == current;
			if (ImGui.Selectable(value.ToString(), selected))
			{
				config.RsrHostileType = RsrSettingsDecision.ClampHostileType(value);
				saveConfig();
			}

			if (selected)
				ImGui.SetItemDefaultFocus();
		}

		ImGui.EndCombo();
	}

	private void DrawRsrTargetingCombo(string label, RsrTargetingType current, Action<RsrTargetingType> set)
	{
		ImGui.SetNextItemWidth(280f);
		if (!ImGui.BeginCombo(label, current.ToString()))
			return;

		foreach (RsrTargetingType value in Enum.GetValues<RsrTargetingType>())
		{
			var selected = value == current;
			if (ImGui.Selectable(value.ToString(), selected))
			{
				set(RsrSettingsDecision.ClampTargetingType(value, current));
				saveConfig();
			}

			if (selected)
				ImGui.SetItemDefaultFocus();
		}

		ImGui.EndCombo();
	}

	private void DrawBossModPreferenceCombo()
	{
		ImGui.SetNextItemWidth(280f);
		var current = BossModCommands.ClampPreference(config.BossModPreference);
		var preview = current switch
		{
			BossModPreference.PreferVbm => "Prefer Boss Mod (vbm)",
			_ => "Prefer BossMod Reborn (BMR)",
		};
		if (!ImGui.BeginCombo("BossMod preference (if both loaded)", preview))
			return;

		foreach (BossModPreference value in Enum.GetValues<BossModPreference>())
		{
			var label = value switch
			{
				BossModPreference.PreferVbm => "Prefer Boss Mod (vbm)",
				_ => "Prefer BossMod Reborn (BMR)",
			};
			var selected = value == current;
			if (ImGui.Selectable(label, selected))
			{
				config.BossModPreference = BossModCommands.ClampPreference(value);
				saveConfig();
			}

			if (selected)
				ImGui.SetItemDefaultFocus();
		}

		ImGui.EndCombo();
	}

	public void Dispose()
	{
	}
}
