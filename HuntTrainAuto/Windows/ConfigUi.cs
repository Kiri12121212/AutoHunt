#nullable enable

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace HuntTrainAuto.Windows;

/// <summary>Small config-window widgets sharing the HuntAlerts NotifyWindow look.</summary>
internal static class ConfigUi
{
	public static void SectionHeader(FontAwesomeIcon icon, string title)
	{
		ImGui.PushStyleColor(ImGuiCol.Text, AlertTheme.Accent);
		ImGui.PushFont(UiBuilder.IconFont);
		ImGui.TextUnformatted(icon.ToIconString());
		ImGui.PopFont();
		ImGui.SameLine();
		ImGui.TextUnformatted(title);
		ImGui.PopStyleColor();
	}

	public static void Subtle(string text)
	{
		// TextUnformatted: HuntAlerts payload text may contain printf specifiers.
		ImGui.PushStyleColor(ImGuiCol.Text, AlertTheme.Subtle);
		ImGui.PushTextWrapPos();
		ImGui.TextUnformatted(text);
		ImGui.PopTextWrapPos();
		ImGui.PopStyleColor();
	}

	public static bool Checkbox(string label, ref bool value, string? tooltip = null)
	{
		var changed = ImGui.Checkbox(label, ref value);
		if (tooltip != null && ImGui.IsItemHovered())
			ImGui.SetTooltip(tooltip);
		return changed;
	}
}
