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
	private readonly IDataManager dataManager;
	private readonly WindowSystem windowSystem;
	private readonly ConfigWindow configWindow;
	private readonly Chat2Ipc chat2Ipc;
	private readonly ChatMessageHandler chatMessageHandler;

	/// <summary>Teleporter IPC; execution owned by phase 3.6 Framework loop.</summary>
	public TeleporterIpc TeleporterIpc { get; }

	/// <summary>Lifestream IPC; instance/fallback TP owned by phase 3.6–3.7.</summary>
	public LifestreamIpc LifestreamIpc { get; }

	public Configuration Config { get; }

	public Plugin(
		IDalamudPluginInterface pluginInterface,
		ICommandManager commandManager,
		IClientState clientState,
		IDataManager dataManager,
		IChatGui chatGui,
		IGameGui gameGui)
	{
		this.pluginInterface = pluginInterface;
		this.clientState = clientState;
		this.dataManager = dataManager;
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

		chatMessageHandler = new ChatMessageHandler(chatGui, gameGui, Config);

		pluginInterface.UiBuilder.Draw += Draw;
		pluginInterface.UiBuilder.OpenConfigUi += ToggleUi;
		clientState.TerritoryChanged += OnTerritoryChanged;

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

	private void OnTerritoryChanged(uint territoryId)
	{
		if (HuntingTerritory.IsHuntingTerritory(territoryId, GetIntendedUseRowId))
			return;

		ConductorList.Clear(Config.Conductors);
		pluginInterface.SavePluginConfig(Config);
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
		clientState.TerritoryChanged -= OnTerritoryChanged;
		chatMessageHandler.Dispose();
		LifestreamIpc.Dispose();
		TeleporterIpc.Dispose();
		chat2Ipc.Dispose();
		pluginInterface.UiBuilder.Draw -= Draw;
		pluginInterface.UiBuilder.OpenConfigUi -= ToggleUi;
		windowSystem.RemoveAllWindows();
		configWindow.Dispose();
	}
}
