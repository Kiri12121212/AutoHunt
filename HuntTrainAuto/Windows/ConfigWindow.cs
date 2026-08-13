#nullable enable

using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using HuntTrainAuto.Domain;
using HuntTrainAuto.HuntAlerts;

namespace HuntTrainAuto.Windows;

public sealed class ConfigWindow : Window, IDisposable
{
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
	private readonly Func<string?>? startFakeHuntNear;
	private readonly Func<string?>? startFakeHuntFar;
	private readonly Func<string?>? startFakeHuntMapFlag;
	private readonly Func<string?>? startFakeHuntInstanceSwap;
	private readonly Func<string?>? endFakeHuntCombat;
	private readonly Func<string?>? clearFakeHunt;
	private readonly Func<HuntTrainMessage?, string?>? startJoinHunt;
	private readonly Func<string>? getJoinHuntStatus;
	private readonly bool isDev;
	private string? fakeHuntStatusMessage;
	private string? joinHuntStatusMessage;
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
		Action? openAlertInfo = null,
		Func<string?>? startFakeHuntNear = null,
		Func<string?>? startFakeHuntFar = null,
		Func<string?>? startFakeHuntMapFlag = null,
		Func<string?>? startFakeHuntInstanceSwap = null,
		Func<string?>? endFakeHuntCombat = null,
		Func<string?>? clearFakeHunt = null,
		Func<HuntTrainMessage?, string?>? startJoinHunt = null,
		Func<string>? getJoinHuntStatus = null,
		bool isDev = false) : base(PluginVersion.WindowTitle)
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
		this.startFakeHuntNear = startFakeHuntNear;
		this.startFakeHuntFar = startFakeHuntFar;
		this.startFakeHuntMapFlag = startFakeHuntMapFlag;
		this.startFakeHuntInstanceSwap = startFakeHuntInstanceSwap;
		this.endFakeHuntCombat = endFakeHuntCombat;
		this.clearFakeHunt = clearFakeHunt;
		this.startJoinHunt = startJoinHunt;
		this.getJoinHuntStatus = getJoinHuntStatus;
		this.isDev = isDev;
		SizeConstraints = new WindowSizeConstraints
		{
			MinimumSize = new Vector2(480, 420),
			MaximumSize = new Vector2(900, 700),
		};
	}

	public override void Draw()
	{
		DrawHeader();
		ImGui.Spacing();
		ImGui.Separator();
		ImGui.Spacing();

		selectedTab = ConfigTabs.ClampSelected(selectedTab, isDev);
		if (!ImGui.BeginTabBar("##htaConfigTabs"))
			return;

		if (ImGui.BeginTabItem(ConfigTabs.Labels[ConfigTabs.Status]))
		{
			selectedTab = ConfigTabs.Status;
			DrawStatusTab();
			ImGui.EndTabItem();
		}

		if (ImGui.BeginTabItem(ConfigTabs.Labels[ConfigTabs.Hunt]))
		{
			selectedTab = ConfigTabs.Hunt;
			DrawHuntTab();
			ImGui.EndTabItem();
		}

		if (ImGui.BeginTabItem(ConfigTabs.Labels[ConfigTabs.Combat]))
		{
			selectedTab = ConfigTabs.Combat;
			DrawCombatTab();
			ImGui.EndTabItem();
		}

		if (ImGui.BeginTabItem(ConfigTabs.Labels[ConfigTabs.Plugins]))
		{
			selectedTab = ConfigTabs.Plugins;
			DrawPluginsTab();
			ImGui.EndTabItem();
		}

		if (ConfigTabs.IsDebugVisible(isDev)
		    && ImGui.BeginTabItem(ConfigTabs.Labels[ConfigTabs.Debug]))
		{
			selectedTab = ConfigTabs.Debug;
			DrawDebugTab();
			ImGui.EndTabItem();
		}

		ImGui.EndTabBar();
	}

	private void DrawHeader()
	{
		DrawEnabledToggle();

		ImGui.Spacing();
		ConfigUi.SectionHeader(FontAwesomeIcon.Users, "Conductors");
		ImGui.SameLine();
		ImGui.TextDisabled($"({config.Conductors.Count})");
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

		ImGui.TextUnformatted("Add conductor:");
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

		DrawJoinHuntButton();
	}

	private void DrawEnabledToggle()
	{
		var enabled = config.Enabled;
		ImGui.PushStyleColor(ImGuiCol.Text, enabled ? AlertTheme.EnabledOn : AlertTheme.EnabledOff);
		ImGui.PushFont(UiBuilder.IconFont);
		ImGui.TextUnformatted((enabled ? FontAwesomeIcon.CheckCircle : FontAwesomeIcon.Circle).ToIconString());
		ImGui.PopFont();
		ImGui.SameLine();
		if (ImGui.Checkbox("Enabled", ref enabled))
		{
			config.Enabled = enabled;
			saveConfig();
		}

		ImGui.PopStyleColor();
		if (ImGui.IsItemHovered())
		{
			ImGui.SetTooltip(enabled
				? "HuntTrainAuto is listening for conductor flags and HuntAlerts."
				: "Turn on to follow the train.");
		}
	}

	private void DrawJoinHuntButton()
	{
		if (startJoinHunt == null)
			return;

		ImGui.Spacing();
		var msg = SafeGetHuntAlertsLastMessage();
		ConfigUi.Subtle(AlertInfoDisplay.FormatStatusSummary(msg));

		if (AlertComponents.ActionButton(FontAwesomeIcon.LocationArrow, "Go to hunt + find conductor", AlertButtonRole.Success))
			joinHuntStatusMessage = startJoinHunt(msg);
		if (ImGui.IsItemHovered())
		{
			ImGui.SetTooltip(
				"Teleport to the last HuntAlerts world + start aetheryte. "
				+ "After landing, runs /sea first <conductor> and assigns them.");
		}

		var joinStatus = joinHuntStatusMessage ?? getJoinHuntStatus?.Invoke();
		if (!string.IsNullOrEmpty(joinStatus))
			ConfigUi.Subtle(joinStatus);
	}

	private void DrawStatusTab()
	{
		ConfigUi.Subtle("Live pipeline — what the train is doing right now.");
		ImGui.Spacing();
		ImGui.TextDisabled(PluginVersion.StatusLine);
		ImGui.Spacing();

		var snap = StatusDisplay.SafeCapture(getStatus);
		AlertComponents.PhaseBadge(snap.Phase);
		ImGui.SameLine();
		ImGui.TextDisabled("phase");
		ImGui.Spacing();

		ImGui.TextUnformatted(StatusDisplay.FormatMountLine(
			snap.Mounted,
			snap.MountPipeline,
			snap.UnmountPipeline));
		ImGui.TextUnformatted(StatusDisplay.FormatNavLine(
			snap.NavPathRunning,
			snap.NavWaypoints,
			snap.NavPathfindInProgress));
		AlertComponents.AvailabilityLine(
			StatusDisplay.FormatBossModAvailable(snap.BossModAvailable, snap.BossModProviderName),
			snap.BossModAvailable);
		ImGui.TextUnformatted(StatusDisplay.FormatBossModAi(snap.BossModAiActive));

		if (isDev)
			ImGui.TextUnformatted(StatusDisplay.FormatFakeHuntLine(
				snap.FakeHuntActive,
				snap.FakeARankSet,
				snap.FakeHuntSummary));

		ImGui.Spacing();
		ImGui.Separator();
		ImGui.Spacing();
		ConfigUi.SectionHeader(FontAwesomeIcon.Flag, "Last HuntAlerts");
		var lastMessage = SafeGetHuntAlertsLastMessage();
		ConfigUi.Subtle(AlertInfoDisplay.FormatStatusSummary(lastMessage));
		if (lastMessage != null && openAlertInfo != null
		    && AlertComponents.ActionButton(FontAwesomeIcon.InfoCircle, "Open alert info", AlertButtonRole.Info))
			openAlertInfo();
	}

	private void DrawHuntTab()
	{
		ConfigUi.SectionHeader(FontAwesomeIcon.Flag, "Flags & travel");
		ImGui.Spacing();
		DrawBoolOption(
			"Teleport to conductor flags",
			() => config.AutoTeleport,
			v => config.AutoTeleport = v,
			"World-hop and same-zone TP when a conductor places a flag.");
		DrawBoolOption(
			"Open map on flags",
			() => config.AutoOpenMap,
			v => config.AutoOpenMap = v);
		DrawBoolOption(
			"Add conductor from context menu",
			() => config.ContextMenu,
			v => config.ContextMenu = v,
			"Right-click a player → Add as conductor.");

		ImGui.Spacing();
		var skipDist = config.AutoTeleportAetheryteDistanceDiff;
		ImGui.SetNextItemWidth(220f);
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

		if (ImGui.IsItemHovered())
			ImGui.SetTooltip("If you are already close enough to the flag aetheryte, skip teleporting.");

		var delayEnabled = config.TeleportDelayEnabled;
		if (ConfigUi.Checkbox(
			    "Random pre-delay before TP",
			    ref delayEnabled,
			    "Adds a short random wait before teleporting."))
		{
			config.TeleportDelayEnabled = delayEnabled;
			saveConfig();
		}

		DrawTeleportDelaySection();

		ImGui.Spacing();
		ImGui.Separator();
		ImGui.Spacing();
		ConfigUi.SectionHeader(FontAwesomeIcon.Bell, "Notifications");
		ImGui.Spacing();
		DrawBoolOption(
			"Toast when a flag lands",
			() => config.EnableNotifications,
			v => config.EnableNotifications = v);
		DrawBoolOption(
			"Play a sound",
			() => config.EnableNotificationSound,
			v => config.EnableNotificationSound = v);
		DrawBoolOption(
			"Open alert card on HuntAlerts map",
			() => config.ShowHuntAlertsInfoWindow,
			v => config.ShowHuntAlertsInfoWindow = v);
		DrawBoolOption(
			"Chat notice (click for info)",
			() => config.ShowHuntAlertsChatNotice,
			v => config.ShowHuntAlertsChatNotice = v);

		ImGui.Spacing();
		ImGui.Separator();
		ImGui.Spacing();
		ConfigUi.SectionHeader(FontAwesomeIcon.Horse, "At the flag");
		ImGui.Spacing();
		DrawBoolOption(
			"Dismount at the flag",
			() => config.AutoUnmountAtFlag,
			v => config.AutoUnmountAtFlag = v);
		DrawBoolOption(
			"Join hunt Party Finder at the flag",
			() => config.AutoJoinHuntPf,
			v => config.AutoJoinHuntPf = v);
		DrawBoolOption(
			"Leave party when the train ends",
			() => config.AutoLeaveHuntParty,
			v => config.AutoLeaveHuntParty = v);

		ImGui.Spacing();
		ImGui.Separator();
		ImGui.Spacing();
		DrawHuntAlertsIntakeSection();
	}

	private void DrawTeleportDelaySection()
	{
		if (!ImGui.CollapsingHeader("Teleport delay"))
			return;

		ImGui.Indent();
		ImGui.BeginDisabled(!config.TeleportDelayEnabled);
		var delayMin = config.TeleportDelayMin;
		ImGui.SetNextItemWidth(220f);
		if (ImGui.SliderInt("Min (ms)", ref delayMin, ConfigTabs.MinTeleportDelayMs, ConfigTabs.MaxTeleportDelayMs))
		{
			var (min, max) = ConfigTabs.ClampTeleportDelayRange(delayMin, config.TeleportDelayMax);
			config.TeleportDelayMin = min;
			config.TeleportDelayMax = max;
			saveConfig();
		}

		var delayMax = config.TeleportDelayMax;
		ImGui.SetNextItemWidth(220f);
		if (ImGui.SliderInt("Max (ms)", ref delayMax, ConfigTabs.MinTeleportDelayMs, ConfigTabs.MaxTeleportDelayMs))
		{
			var (min, max) = ConfigTabs.ClampTeleportDelayRange(config.TeleportDelayMin, delayMax);
			config.TeleportDelayMin = min;
			config.TeleportDelayMax = max;
			saveConfig();
		}

		ImGui.EndDisabled();
		ImGui.Unindent();
	}

	private void DrawHuntAlertsIntakeSection()
	{
		if (!ImGui.CollapsingHeader("HuntAlerts intake", ImGuiTreeNodeFlags.DefaultOpen))
			return;

		ImGui.Indent();
		DrawBoolOption(
			"Enable HuntAlerts integration",
			() => config.HuntAlertsIntegration,
			v => config.HuntAlertsIntegration = v,
			"When a hunt mark arrives, teleport to the target world and zone.");
		ImGui.BeginDisabled(!config.HuntAlertsIntegration);
		DrawBoolOption(
			"Auto-assign conductor from HuntAlerts message",
			() => config.HuntAlertsAutoConductor,
			v => config.HuntAlertsAutoConductor = v,
			"Parses \"Conductor - Name\" / \"Conductor: [World] Name\" from toast text.");

		ImGui.Spacing();
		ImGui.TextUnformatted("Accept ranks");
		DrawHuntAlertsRankCheckbox("A-rank trains (new_hunt)", HuntMarkRank.A);
		DrawHuntAlertsRankCheckbox("S-rank alerts (srank)", HuntMarkRank.S);

		ImGui.Spacing();
		ImGui.TextUnformatted("Accept expansions");
		if (ImGui.IsItemHovered())
			ImGui.SetTooltip("Empty = all. Prefers start-zone ExVersion; else HuntAlerts huntKind.");
		foreach (var group in HuntAlertsFilter.TrainGroups.All)
			DrawHuntAlertsTrainGroupCheckbox(group);
		ImGui.EndDisabled();
		ImGui.Unindent();
	}

	private void DrawCombatTab()
	{
		ConfigUi.Subtle(
			"Path to the flag, engage nearby A-ranks, then RSR handles rotation while BossMod AI handles movement.");
		ImGui.Spacing();

		var engageRange = config.EngageRange;
		ImGui.SetNextItemWidth(220f);
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

		if (ImGui.IsItemHovered())
			ImGui.SetTooltip("vnav stops pathing at this distance; BossMod closes in for melee.");

		var aRankScan = config.ARankScanRange;
		ImGui.SetNextItemWidth(220f);
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

		if (ImGui.IsItemHovered())
			ImGui.SetTooltip("Search radius around the flag after unmount.");

		DrawBoolOption(
			"Prefer A-rank nearest hunt hint",
			() => config.PreferARankNearHuntHint,
			v => config.PreferARankNearHuntHint = v,
			"Bias toward the last conductor / HuntAlerts map position when several A-ranks are in range.");

		ImGui.Spacing();
		ImGui.Separator();
		ImGui.Spacing();
		ConfigUi.SectionHeader(FontAwesomeIcon.Crosshairs, "Rotation & AI");
		ImGui.Spacing();
		DrawRsrHostileCombo();
		DrawRsrTargetingCombo("Tank targeting", config.RsrTargetingTank, v => config.RsrTargetingTank = v);
		DrawRsrTargetingCombo("Non-tank targeting", config.RsrTargetingNonTank, v => config.RsrTargetingNonTank = v);

		ImGui.Spacing();
		var bm = config.BossModIntegration;
		if (ConfigUi.Checkbox(
			    "Enable BossMod AI in combat",
			    ref bm,
			    "HTA enables BM AI and forbids conflicting casts while RSR runs the GCD."))
		{
			config.BossModIntegration = bm;
			saveConfig();
		}

		ImGui.BeginDisabled(!config.BossModIntegration);
		DrawBossModPreferenceCombo();
		ImGui.EndDisabled();
	}

	private void DrawPluginsTab()
	{
		ConfigUi.SectionHeader(FontAwesomeIcon.Plug, "Dependencies");
		ImGui.Spacing();
		DrawDependencyLine(DependencyAvailability.TeleporterDisplayName, teleporterAvailable);
		DrawDependencyLine(DependencyAvailability.LifestreamDisplayName, lifestreamAvailable);
		DrawDependencyLine(DependencyAvailability.VnavmeshDisplayName, vnavmeshAvailable);
		DrawDependencyLine(DependencyAvailability.RsrDisplayName, rsrAvailable);
		DrawDependencyLine(DependencyAvailability.BossModDisplayName, bossModAvailable);
		DrawHuntAlertsAvailabilityLine();

		ImGui.Spacing();
		ImGui.Separator();
		ImGui.Spacing();
		ConfigUi.SectionHeader(FontAwesomeIcon.Bullhorn, "HuntAlerts feed");
		ImGui.Spacing();
		ConfigUi.Subtle(HuntAlertsAvailability.FormatLastAlertStatus(SafeGetHuntAlertsLastAlert()));
		ConfigUi.Subtle(HuntAlertsAvailability.FormatLastIntakeStatus(SafeGetHuntAlertsLastIntake()));
		if (openAlertInfo != null
		    && AlertComponents.ActionButton(FontAwesomeIcon.InfoCircle, "Open HuntAlerts alert info", AlertButtonRole.Info))
			openAlertInfo();
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
		var available = status == HuntAlertsPluginStatus.Available;
		AlertComponents.AvailabilityLine(
			HuntAlertsAvailability.FormatAvailabilityLine(status),
			available);
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
		if (ConfigUi.Checkbox("Record automation events", ref debugLogging))
		{
			config.EnableDebugLogging = debugLogging;
			saveConfig();
		}

		ImGui.SameLine();
		if (ImGui.SmallButton("Clear log"))
			debugLog.Clear();

		ImGui.Spacing();
		ImGui.Separator();
		ConfigUi.SectionHeader(FontAwesomeIcon.Flask, "Fake Hunt");
		ConfigUi.Subtle(
			"Inject a conductor flag + synthetic A-rank for mount/TP/nav/combat testing. "
			+ "Plugin must be Enabled.");
		ImGui.Spacing();

		if (ImGui.Button("Near (~250y)"))
			fakeHuntStatusMessage = FormatFakeHuntUiResult(
				startFakeHuntNear?.Invoke(),
				"OK: Fake Hunt armed — watch Status / movement");
		ImGui.SameLine();
		if (ImGui.Button("Far (~1000y)"))
			fakeHuntStatusMessage = FormatFakeHuntUiResult(
				startFakeHuntFar?.Invoke(),
				"OK: Fake Hunt armed — watch Status / movement");
		ImGui.SameLine();
		if (ImGui.Button("Map flag"))
			fakeHuntStatusMessage = FormatFakeHuntUiResult(
				startFakeHuntMapFlag?.Invoke(),
				"OK: Fake Hunt armed — watch Status / movement");
		ImGui.SameLine();
		if (ImGui.Button("Instance swap"))
			fakeHuntStatusMessage = FormatFakeHuntUiResult(
				startFakeHuntInstanceSwap?.Invoke(),
				"OK: Fake Hunt instance swap — watch SwitchInstance / ChangeInstance");

		if (ImGui.Button("End fake combat"))
			fakeHuntStatusMessage = FormatFakeHuntUiResult(
				endFakeHuntCombat?.Invoke(),
				"OK: Fake combat ended");
		ImGui.SameLine();
		if (ImGui.Button("Clear fake hunt"))
			fakeHuntStatusMessage = FormatFakeHuntUiResult(
				clearFakeHunt?.Invoke(),
				"OK: Fake Hunt cleared");

		if (!string.IsNullOrEmpty(fakeHuntStatusMessage))
		{
			AlertComponents.AvailabilityLine(
				fakeHuntStatusMessage,
				fakeHuntStatusMessage.StartsWith("OK:", StringComparison.Ordinal));
		}
		else
		{
			var snap = StatusDisplay.SafeCapture(getStatus);
			ImGui.TextDisabled(StatusDisplay.FormatFakeHuntLine(
				snap.FakeHuntActive,
				snap.FakeARankSet,
				snap.FakeHuntSummary));
		}

		ImGui.Spacing();
		ImGui.Separator();
		ImGui.TextDisabled($"Recent events ({debugLog.Count}/{debugLog.Capacity}, newest first)");
		ImGui.Spacing();

		var events = debugLog.SnapshotNewestFirst();
		if (events.Count == 0)
		{
			ImGui.TextDisabled("(empty)");
			return;
		}

		var height = Math.Clamp(events.Count, 4, 16) * ImGui.GetTextLineHeightWithSpacing();
		if (ImGui.BeginChild("##htaDebugLog", new Vector2(-1, height), border: true))
		{
			foreach (var e in events)
				ImGui.TextUnformatted(DebugEventFormatter.FormatLine(e));
		}

		ImGui.EndChild();
	}

	private void DrawBoolOption(string label, Func<bool> getter, Action<bool> setter, string? tooltip = null)
	{
		var value = getter();
		if (!ConfigUi.Checkbox(label, ref value, tooltip))
			return;

		setter(value);
		saveConfig();
	}

	private static string FormatFakeHuntUiResult(string? errorOrNull, string okMessage)
		=> errorOrNull ?? okMessage;

	private static void DrawDependencyLine(string displayName, Func<bool> probe)
	{
		var available = DependencyAvailability.SafeIsAvailable(probe);
		AlertComponents.AvailabilityLine(
			DependencyAvailability.FormatLine(displayName, available),
			available);
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
		if (!ImGui.BeginCombo("BossMod preference (if both loaded)", FormatBossModPreference(current)))
			return;

		foreach (BossModPreference value in Enum.GetValues<BossModPreference>())
		{
			var selected = value == current;
			if (ImGui.Selectable(FormatBossModPreference(value), selected))
			{
				config.BossModPreference = BossModCommands.ClampPreference(value);
				saveConfig();
			}

			if (selected)
				ImGui.SetItemDefaultFocus();
		}

		ImGui.EndCombo();
	}

	private static string FormatBossModPreference(BossModPreference preference)
		=> preference switch
		{
			BossModPreference.PreferVbm => "Prefer Boss Mod (vbm)",
			_ => "Prefer BossMod Reborn (BMR)",
		};

	public void Dispose()
	{
	}
}
