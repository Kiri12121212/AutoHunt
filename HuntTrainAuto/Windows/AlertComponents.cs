#nullable enable

using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;

namespace HuntTrainAuto.Windows;

/// <summary>ImGui widgets matching HuntAlerts NotifyWindow badges / rows / actions.</summary>
internal static class AlertComponents
{
	public static void Badge(string text, AlertBadgeStyle style)
	{
		if (string.IsNullOrWhiteSpace(text))
			return;

		var (bg, border, fg) = style switch
		{
			AlertBadgeStyle.SRank => (AlertTheme.SRankBg, AlertTheme.SRankBorder, AlertTheme.SRankText),
			AlertBadgeStyle.Train => (AlertTheme.TrainBg, AlertTheme.TrainBorder, AlertTheme.TrainText),
			AlertBadgeStyle.World => (AlertTheme.WorldBg, AlertTheme.WorldBorder, AlertTheme.WorldText),
			_ => (AlertTheme.KindBg, AlertTheme.KindBorder, AlertTheme.KindText),
		};

		var pad = AlertTheme.BadgePadding;
		var draw = ImGui.GetWindowDrawList();
		var size = ImGui.CalcTextSize(text);
		var start = ImGui.GetCursorScreenPos();
		var end = new Vector2(start.X + size.X + pad.X * 2, start.Y + size.Y + pad.Y * 2);
		draw.AddRectFilled(start, end, bg, 2f);
		draw.AddRect(start, end, border, 2f);
		draw.AddText(new Vector2(start.X + pad.X, start.Y + pad.Y), fg, text);
		ImGui.Dummy(new Vector2(size.X + pad.X * 2, size.Y + pad.Y * 2));
	}

	public static void FieldRow(string label, string value)
	{
		ImGui.PushStyleColor(ImGuiCol.Text, AlertTheme.Accent);
		ImGui.TextUnformatted(label);
		ImGui.PopStyleColor();
		ImGui.SameLine(90f);
		ImGui.TextUnformatted(value);
	}

	public static bool ActionButton(FontAwesomeIcon icon, string label, AlertButtonRole role)
	{
		var (b, h, a) = role switch
		{
			AlertButtonRole.Success => (AlertTheme.SuccessBtn, AlertTheme.SuccessBtnHover, AlertTheme.SuccessBtnActive),
			_ => (AlertTheme.InfoBtn, AlertTheme.InfoBtnHover, AlertTheme.InfoBtnActive),
		};
		ImGui.PushStyleColor(ImGuiCol.Button, b);
		ImGui.PushStyleColor(ImGuiCol.ButtonHovered, h);
		ImGui.PushStyleColor(ImGuiCol.ButtonActive, a);
		var clicked = ImGuiComponents.IconButtonWithText(icon, label);
		ImGui.PopStyleColor(3);
		return clicked;
	}
}
