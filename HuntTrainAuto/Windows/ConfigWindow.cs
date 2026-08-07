using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace HuntTrainAuto.Windows;

public sealed class ConfigWindow : Window, IDisposable
{
	private readonly Configuration config;

	public ConfigWindow(Configuration config) : base("HuntTrainAuto")
	{
		this.config = config;
		SizeConstraints = new WindowSizeConstraints
		{
			MinimumSize = new Vector2(400, 200),
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
	}

	public void Dispose()
	{
	}
}
