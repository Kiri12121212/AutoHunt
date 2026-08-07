using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using HuntTrainAuto.Services;
using HuntTrainAuto.Windows;

namespace HuntTrainAuto;

public sealed class Plugin : IDalamudPlugin
{
	private readonly IDalamudPluginInterface pluginInterface;
	private readonly WindowSystem windowSystem;
	private readonly ConfigWindow configWindow;
	private readonly Chat2Ipc chat2Ipc;

	public Configuration Config { get; }

	public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commandManager)
	{
		this.pluginInterface = pluginInterface;
		Config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

		windowSystem = new WindowSystem(typeof(Plugin).Assembly.GetName()?.Name ?? "HuntTrainAuto");
		configWindow = new ConfigWindow(Config, () => pluginInterface.SavePluginConfig(Config));
		windowSystem.AddWindow(configWindow);

		chat2Ipc = new Chat2Ipc(
			pluginInterface,
			Config,
			() => pluginInterface.SavePluginConfig(Config),
			() => configWindow.IsOpen = true);

		pluginInterface.UiBuilder.Draw += Draw;
		pluginInterface.UiBuilder.OpenConfigUi += ToggleUi;

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

	private void Draw() => windowSystem.Draw();

	private void ToggleUi()
	{
		configWindow.IsOpen = !configWindow.IsOpen;
		pluginInterface.SavePluginConfig(Config);
	}

	public void Dispose()
	{
		chat2Ipc.Dispose();
		pluginInterface.UiBuilder.Draw -= Draw;
		pluginInterface.UiBuilder.OpenConfigUi -= ToggleUi;
		windowSystem.RemoveAllWindows();
		configWindow.Dispose();
	}
}
