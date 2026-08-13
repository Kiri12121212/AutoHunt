#nullable enable

using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using HuntTrainAuto.Contracts;
using HuntTrainAuto.HuntAlerts;

namespace HuntTrainAuto.Windows;

/// <summary>
/// HuntAlerts NotifyWindow-style detail card for the last accepted HuntTrainMessage.
/// </summary>
public sealed class AlertInfoWindow : Window, IDisposable
{
	private readonly Func<HuntTrainMessage?> getMessage;
	private readonly IChatOutput chat;
	private readonly Action? openConfig;
	private readonly Func<HuntTrainMessage?, string?>? startJoinHunt;
	private readonly Func<string>? getJoinHuntStatus;
	private HuntTrainMessage? overrideMessage;
	private string defaultRelayChannel = AlertRelay.DefaultChannel;
	private string? joinHuntStatusMessage;

	public AlertInfoWindow(
		Func<HuntTrainMessage?> getMessage,
		IChatOutput chat,
		Action? openConfig = null,
		Func<HuntTrainMessage?, string?>? startJoinHunt = null,
		Func<string>? getJoinHuntStatus = null)
		: base("HuntTrainAuto Notification", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
	{
		this.getMessage = getMessage ?? throw new ArgumentNullException(nameof(getMessage));
		this.chat = chat ?? throw new ArgumentNullException(nameof(chat));
		this.openConfig = openConfig;
		this.startJoinHunt = startJoinHunt;
		this.getJoinHuntStatus = getJoinHuntStatus;

		Size = new Vector2(540, 380);
		SizeCondition = ImGuiCond.FirstUseEver;
		SizeConstraints = new WindowSizeConstraints
		{
			MinimumSize = new Vector2(520, 240),
			MaximumSize = new Vector2(1000, 1400),
		};

		TitleBarButtons.Add(new TitleBarButton
		{
			Icon = FontAwesomeIcon.Cog,
			IconOffset = new Vector2(2, 1),
			Click = _ => openConfig?.Invoke(),
			ShowTooltip = () => ImGui.SetTooltip("Open settings"),
		});
	}

	/// <summary>Show this window for the current retained HuntAlerts payload.</summary>
	public void ShowLatest()
	{
		overrideMessage = null;
		IsOpen = true;
	}

	/// <summary>Show this window for a specific cached HuntAlerts payload (chat link click).</summary>
	public void Show(HuntTrainMessage message)
	{
		ArgumentNullException.ThrowIfNull(message);
		overrideMessage = message;
		IsOpen = true;
	}

	public override void Draw()
	{
		HuntTrainMessage? entry = overrideMessage;
		if (entry == null)
		{
			try
			{
				entry = getMessage();
			}
			catch
			{
				// soft-fail
			}
		}

		if (entry == null)
		{
			ImGui.PushStyleColor(ImGuiCol.Text, AlertTheme.Subtle);
			ImGui.TextWrapped("No HuntAlerts payload yet. Enable integration and wait for a train/S alert.");
			ImGui.PopStyleColor();
			return;
		}

		var isTrain = AlertInfoDisplay.IsTrain(entry);
		DrawHero(entry, isTrain);
		ImGui.Spacing();
		DrawTagStrip(entry);
		ImGui.Spacing();

		var footerHeight = ImGui.GetFrameHeightWithSpacing() + ImGui.GetStyle().ItemSpacing.Y * 2;
		var midHeight = Math.Max(40f, ImGui.GetContentRegionAvail().Y - footerHeight);

		if (ImGui.BeginChild("##htaAlertMid", new Vector2(0, midHeight), false))
		{
			// HuntAlerts NotifyWindow: trains keep Kind/Hunt/Zone inside Message;
			// S-rank uses FieldRows + free-text Message.
			if (isTrain)
				DrawMessageBody(entry);
			else
				DrawStructuredFields(entry);
		}

		ImGui.EndChild();

		ImGui.Separator();
		DrawActions(entry);
	}

	private static void DrawHero(HuntTrainMessage entry, bool isTrain)
	{
		AlertComponents.Badge(
			AlertInfoDisplay.FormatBadge(entry),
			isTrain ? AlertBadgeStyle.Train : AlertBadgeStyle.SRank);
		ImGui.SameLine();
		ImGui.TextUnformatted(AlertInfoDisplay.FormatTitle(entry));
	}

	private static void DrawTagStrip(HuntTrainMessage entry)
	{
		if (!string.IsNullOrWhiteSpace(entry.huntKind))
		{
			AlertComponents.Badge(entry.huntKind.Trim(), AlertBadgeStyle.Kind);
			ImGui.SameLine();
		}

		if (!string.IsNullOrWhiteSpace(entry.huntWorld))
			AlertComponents.Badge(entry.huntWorld.Trim(), AlertBadgeStyle.World);

		if (entry.instance > 1)
		{
			ImGui.SameLine();
			AlertComponents.Badge($"i{entry.instance}", AlertBadgeStyle.Kind);
		}
	}

	private static void DrawStructuredFields(HuntTrainMessage entry)
	{
		if (!string.IsNullOrWhiteSpace(entry.creatureName))
			AlertComponents.FieldRow("Creature", entry.creatureName.Trim());
		if (!string.IsNullOrWhiteSpace(entry.startZone))
			AlertComponents.FieldRow("Zone", entry.startZone.Trim());
		if (!string.IsNullOrWhiteSpace(entry.locationCoords))
			AlertComponents.FieldRow("Coords", entry.locationCoords.Trim());
		if (!string.IsNullOrWhiteSpace(entry.startLocation))
			AlertComponents.FieldRow("Aetheryte", entry.startLocation.Trim());
		var posted = AlertInfoDisplay.FormatPosted(entry);
		if (!string.Equals(posted, AlertInfoDisplay.EmptyLabel, StringComparison.Ordinal))
			AlertComponents.FieldRow("Posted", posted);

		ImGui.Spacing();
		DrawMessageBody(entry, subtle: true);
	}

	private static void DrawMessageBody(HuntTrainMessage entry, bool subtle = false)
	{
		if (string.IsNullOrWhiteSpace(entry.Message))
			return;

		ImGui.PushStyleColor(ImGuiCol.Text, subtle ? AlertTheme.Subtle : AlertTheme.Text);
		ImGui.PushTextWrapPos();
		ImGui.TextUnformatted(entry.Message);
		ImGui.PopTextWrapPos();
		ImGui.PopStyleColor();
	}

	private void DrawActions(HuntTrainMessage entry)
	{
		if (startJoinHunt != null
		    && AlertComponents.ActionButton(FontAwesomeIcon.LocationArrow, "Go + conductor", AlertButtonRole.Success))
			joinHuntStatusMessage = startJoinHunt(entry);
		if (startJoinHunt != null && ImGui.IsItemHovered())
		{
			ImGui.SetTooltip(
				"Teleport to this hunt's world + start aetheryte. "
				+ "After landing, runs /sea first <conductor> and assigns them when a conductor name is parsed; "
				+ "otherwise search/assign is skipped.");
		}

		if (startJoinHunt != null)
			ImGui.SameLine();

		if (AlertComponents.ActionButton(FontAwesomeIcon.Users, "Party Finder", AlertButtonRole.Info))
			chat.TryExecuteCommand("/partyfinder");

		ImGui.SameLine();
		var defaultDisplay = AlertRelay.DisplayFor(defaultRelayChannel);
		if (AlertComponents.ActionButton(FontAwesomeIcon.Bullhorn, "Relay", AlertButtonRole.Success))
			TryRelay(entry, defaultRelayChannel);
		if (ImGui.IsItemHovered())
			ImGui.SetTooltip($"Relay to {defaultDisplay}");

		ImGui.SameLine(0, 2);
		ImGui.PushStyleColor(ImGuiCol.Button, AlertTheme.SuccessBtn);
		ImGui.PushStyleColor(ImGuiCol.ButtonHovered, AlertTheme.SuccessBtnHover);
		ImGui.PushStyleColor(ImGuiCol.ButtonActive, AlertTheme.SuccessBtnActive);
		if (ImGuiComponents.IconButton("##htaRelayChevron", FontAwesomeIcon.ChevronUp))
			ImGui.OpenPopup("##htaRelayPicker");
		ImGui.PopStyleColor(3);
		if (ImGui.IsItemHovered())
			ImGui.SetTooltip("Pick a one-off relay channel");

		if (ImGui.BeginPopup("##htaRelayPicker"))
		{
			ImGui.PushStyleColor(ImGuiCol.Text, AlertTheme.Subtle);
			ImGui.TextUnformatted($"Default: {defaultDisplay}");
			ImGui.PopStyleColor();
			ImGui.Separator();
			foreach (var ch in AlertRelay.Channels)
			{
				if (!ImGui.MenuItem($"{ch.Display}  {ch.Command}"))
					continue;
				defaultRelayChannel = ch.Command;
				TryRelay(entry, ch.Command);
			}

			ImGui.EndPopup();
		}

		var joinStatus = joinHuntStatusMessage ?? getJoinHuntStatus?.Invoke();
		if (!string.IsNullOrEmpty(joinStatus))
		{
			ImGui.Spacing();
			ImGui.PushStyleColor(ImGuiCol.Text, AlertTheme.Subtle);
			ImGui.TextWrapped(joinStatus);
			ImGui.PopStyleColor();
		}
	}

	private void TryRelay(HuntTrainMessage entry, string channel)
	{
		var line = AlertRelay.BuildChatCommand(entry, channel);
		if (string.IsNullOrEmpty(line))
			return;
		chat.TryExecuteCommand(line);
	}

	public void Dispose()
	{
		// WindowSystem owns removal; nothing else to free.
	}
}
