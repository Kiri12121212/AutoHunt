using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using HuntTrainAuto.Windows;

namespace HuntTrainAuto;

public sealed class Plugin : IDalamudPlugin
{
	private readonly IDalamudPluginInterface pluginInterface;
	private readonly WindowSystem windowSystem;
	private readonly ConfigWindow configWindow;

	public Configuration Config { get; }

	public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commandManager)
	{
		this.pluginInterface = pluginInterface;
		Config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

		windowSystem = new WindowSystem(typeof(Plugin).Assembly.GetName()?.Name ?? "HuntTrainAuto");
		configWindow = new ConfigWindow(Config, () => pluginInterface.SavePluginConfig(Config));
		windowSystem.AddWindow(configWindow);

		pluginInterface.UiBuilder.Draw += Draw;
		pluginInterface.UiBuilder.OpenConfigUi += ToggleUi;

		commandManager.AddHandler("/hta", new CommandInfo(OnCommand)
		{
			HelpMessage = "Toggle the HuntTrainAuto window",
		});
	}

	public string Name => "HuntTrainAuto";

	private void OnCommand(string command, string args) => ToggleUi();

	private void Draw() => windowSystem.Draw();

	private void ToggleUi()
	{
		configWindow.IsOpen = !configWindow.IsOpen;
		pluginInterface.SavePluginConfig(Config);
	}

	public void Dispose()
	{
		pluginInterface.UiBuilder.Draw -= Draw;
		pluginInterface.UiBuilder.OpenConfigUi -= ToggleUi;
		windowSystem.RemoveAllWindows();
		configWindow.Dispose();
	}
}
