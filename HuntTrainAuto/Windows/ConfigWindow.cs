using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace HuntTrainAuto.Windows;

public sealed class ConfigWindow : Window, IDisposable
{
	private readonly Configuration config;
	private readonly Action saveConfig;
	private string conductorInput = string.Empty;
	private int selectedConductor;

	public ConfigWindow(Configuration config, Action saveConfig) : base("HuntTrainAuto")
	{
		this.config = config;
		this.saveConfig = saveConfig;
		SizeConstraints = new WindowSizeConstraints
		{
			MinimumSize = new Vector2(400, 280),
			MaximumSize = new Vector2(800, 600),
		};
	}

	public override void Draw()
	{
		ImGui.TextWrapped("Vanilla Dalamud scaffold — no ECommons or NightmareUI.");
		ImGui.Text($"Conductors: {config.Conductors.Count}");
		ImGui.Spacing();

		var enabled = config.Enabled;
		if (ImGui.Checkbox("Enabled", ref enabled))
			config.Enabled = enabled;

		var suppressChat = config.SuppressChatOtherPlayers;
		if (ImGui.Checkbox("Suppress chat from other players", ref suppressChat))
			config.SuppressChatOtherPlayers = suppressChat;

		var contextMenu = config.ContextMenu;
		if (ImGui.Checkbox("Context menu", ref contextMenu))
			config.ContextMenu = contextMenu;

		var autoOpenMap = config.AutoOpenMap;
		if (ImGui.Checkbox("Auto-open map on conductor flag", ref autoOpenMap))
			config.AutoOpenMap = autoOpenMap;

		var noDuplicateFlags = config.NoDuplicateFlags;
		if (ImGui.Checkbox("Skip duplicate flags (same zone, ≤10)", ref noDuplicateFlags))
			config.NoDuplicateFlags = noDuplicateFlags;

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

		ImGui.Spacing();
		ImGui.Separator();
		ImGui.Spacing();

		ImGui.Text("Current conductors:");
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

	public void Dispose()
	{
	}
}
