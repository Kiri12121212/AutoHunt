#nullable enable
using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace HuntTrainAuto.Windows;

public sealed class ConfigWindow : Window, IDisposable
{
	private static readonly Vector4 AvailableColor = new(0.45f, 0.85f, 0.45f, 1f);
	private static readonly Vector4 MissingColor = new(0.95f, 0.40f, 0.40f, 1f);
	private static readonly Vector4 PlaceholderColor = new(0.70f, 0.70f, 0.70f, 1f);

	private readonly Configuration config;
	private readonly Action saveConfig;
	private readonly Func<bool> teleporterAvailable;
	private readonly Func<bool> lifestreamAvailable;
	private readonly Func<bool> vnavmeshAvailable;
	private readonly Func<bool> rsrAvailable;
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
		Func<StatusSnapshot> getStatus,
		DebugEventLog debugLog) : base("HuntTrainAuto")
	{
		this.config = config;
		this.saveConfig = saveConfig;
		this.teleporterAvailable = teleporterAvailable;
		this.lifestreamAvailable = lifestreamAvailable;
		this.vnavmeshAvailable = vnavmeshAvailable;
		this.rsrAvailable = rsrAvailable;
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

		if (ImGui.BeginTabItem(ConfigTabs.Labels[ConfigTabs.Follow]))
		{
			selectedTab = ConfigTabs.Follow;
			DrawFollowTab();
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

		var snap = StatusDisplay.SafeCapture(getStatus);
		ImGui.Text(StatusDisplay.FormatPhaseLine(snap.Phase));
		ImGui.Text(StatusDisplay.FormatMountLine(
			snap.Mounted,
			snap.MountPipeline,
			snap.UnmountPipeline));
		ImGui.Text(StatusDisplay.FormatFollowLine(snap.FollowTargetName, snap.FollowEnabled));
		ImGui.Text(StatusDisplay.FormatNavLine(
			snap.NavPathRunning,
			snap.NavWaypoints,
			snap.NavPathfindInProgress));
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
		if (ImGui.Checkbox("Context menu", ref contextMenu))
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

	private void DrawFollowTab()
	{
		var followConductorFirst = config.FollowConductorFirst;
		if (ImGui.Checkbox("Follow conductor first (else party leader)", ref followConductorFirst))
		{
			config.FollowConductorFirst = followConductorFirst;
			saveConfig();
		}

		var followDistance = config.PartyFollowDistance;
		ImGui.SetNextItemWidth(200f);
		if (ImGui.SliderFloat(
			    "Party follow distance (yalms)",
			    ref followDistance,
			    FollowDecision.MinFollowDistance,
			    FollowDecision.MaxFollowDistance,
			    "%.1f"))
		{
			config.PartyFollowDistance = FollowDecision.ClampFollowDistance(followDistance);
			saveConfig();
		}

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
		ImGui.TextWrapped("Rotation Solver Reborn settings applied on engage.");
		ImGui.Spacing();
		DrawRsrHostileCombo();
		DrawRsrTargetingCombo("Tank targeting", config.RsrTargetingTank, v => config.RsrTargetingTank = v);
		DrawRsrTargetingCombo("Non-tank targeting", config.RsrTargetingNonTank, v => config.RsrTargetingNonTank = v);
	}

	private void DrawIntegrationsTab()
	{
		ImGui.Text("Plugin availability:");
		DrawDependencyLine(DependencyAvailability.TeleporterDisplayName, teleporterAvailable);
		DrawDependencyLine(DependencyAvailability.LifestreamDisplayName, lifestreamAvailable);
		DrawDependencyLine(DependencyAvailability.VnavmeshDisplayName, vnavmeshAvailable);
		DrawDependencyLine(DependencyAvailability.RsrDisplayName, rsrAvailable);
		ImGui.TextColored(PlaceholderColor, ConfigTabs.FormatHuntAlertsPlaceholder());
		ImGui.TextDisabled("HuntAlerts intake lands in Phase 10.");
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

	public void Dispose()
	{
	}
}
