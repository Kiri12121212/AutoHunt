#nullable enable
using System;
using System.Collections.Generic;

namespace HuntTrainAuto.Chat;

/// <summary>
/// Pure gates for native player context-menu "Add as conductor" (HTA ContextMenuManager).
/// </summary>
public static class ContextMenuDecision
{
	/// <summary>Addons where HTA shows the conductor item (includes null = world target).</summary>
	public static readonly HashSet<string?> ValidAddons = new(StringComparer.Ordinal)
	{
		null,
		"PartyMemberList",
		"FriendList",
		"FreeCompany",
		"LinkShell",
		"CrossWorldLinkshell",
		"_PartyList",
		"ChatLog",
		"LookingForGroup",
		"BlackList",
		"ContentMemberList",
		"SocialList",
		"ContactList",
	};

	/// <summary>
	/// Whether to inject the menu item. ChatTwo IPC is separate; this is vanilla Dalamud
	/// <c>IContextMenu</c> only. World validity is optional (fail-open when unknown).
	/// </summary>
	public static bool ShouldShow(
		bool contextMenuEnabled,
		string? addonName,
		bool hasPlayerName,
		bool rejectNonPublicWorld = false)
	{
		if (!contextMenuEnabled)
			return false;
		if (!hasPlayerName)
			return false;
		if (rejectNonPublicWorld)
			return false;
		return ValidAddons.Contains(addonName);
	}
}
