#nullable enable
using System;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using HuntTrainAuto.Chat;
using HuntTrainAuto.Logging;
using Lumina.Excel.Sheets;

namespace HuntTrainAuto.Services;

/// <summary>
/// Native game context menu: "Add as conductor" (HTA <c>ContextMenuManager</c> parity).
/// ChatTwo uses <see cref="Chat2Ipc"/> separately.
/// </summary>
public sealed class ContextMenuService : IDisposable
{
	private readonly IContextMenu contextMenu;
	private readonly IDataManager dataManager;
	private readonly IPluginLog pluginLog;
	private readonly Configuration config;
	private readonly System.Action saveConfig;
	private readonly System.Action openConfigUi;
	private readonly MenuItem menuItem;

	public ContextMenuService(
		IContextMenu contextMenu,
		IDataManager dataManager,
		IPluginLog pluginLog,
		Configuration config,
		System.Action saveConfig,
		System.Action openConfigUi)
	{
		this.contextMenu = contextMenu;
		this.dataManager = dataManager;
		this.pluginLog = pluginLog;
		this.config = config;
		this.saveConfig = saveConfig;
		this.openConfigUi = openConfigUi;

		menuItem = new MenuItem
		{
			Name = new SeStringBuilder().AddUiForeground("Add as conductor", 578).Build(),
			Prefix = SeIconChar.BoxedLetterH,
			PrefixColor = 578,
			OnClicked = AssignConductor,
		};

		contextMenu.OnMenuOpened += OnMenuOpened;
	}

	private void OnMenuOpened(IMenuOpenedArgs args)
	{
		try
		{
			if (!config.ContextMenu)
				return;
			if (args.Target is not MenuTargetDefault target)
				return;

			var name = target.TargetName?.Trim() ?? string.Empty;
			if (string.IsNullOrEmpty(name))
				return;

			// HTA ValidAddons (ChatLog, party, friends, null=world, …). No hunt-zone gate —
			// conductors are often set from chat in cities.
			if (!ContextMenuDecision.ValidAddons.Contains(args.AddonName))
				return;

			// Prefer public-world targets; fail-open when sheet/world is unavailable.
			var world = target.TargetHomeWorld;
			if (world.IsValid && !IsPublicWorld(world.RowId))
				return;

			args.AddMenuItem(menuItem);
			Debug($"added conductor action for {name}");
		}
		catch (Exception ex)
		{
			Debug($"menu open soft-fail: {ex.Message}");
		}
	}

	private void AssignConductor(IMenuItemClickedArgs args)
	{
		try
		{
			if (args.Target is not MenuTargetDefault target)
				return;

			var name = target.TargetName?.Trim() ?? string.Empty;
			if (!ConductorList.TryAdd(config.Conductors, name))
			{
				Debug($"conductor action skipped: {name}");
				return;
			}

			saveConfig();
			openConfigUi();
			Debug($"conductor action succeeded: {name}");
		}
		catch (Exception ex)
		{
			Debug($"conductor action soft-fail: {ex.Message}");
		}
	}

	private bool IsPublicWorld(uint worldId)
	{
		try
		{
			var row = dataManager.GetExcelSheet<World>()?.GetRowOrDefault(worldId);
			// Missing row → fail-open (still show). Explicit IsPublic=false → hide.
			return row is null or { IsPublic: true };
		}
		catch
		{
			return true;
		}
	}

	private void Debug(string message)
		=> DebugBehavior.Debug(pluginLog, config.EnableDebugLogging, "Chat2", message);

	public void Dispose() => contextMenu.OnMenuOpened -= OnMenuOpened;
}
