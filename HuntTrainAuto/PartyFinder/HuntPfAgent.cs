#nullable enable
using System;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace HuntTrainAuto.PartyFinder;

/// <summary>
/// Best-effort unsafe AgentLookingForGroup / LookingForGroupDetail join path.
/// Soft-fails; never throws. Join-button click mirrors ECommons ClickAddonButton
/// (ReceiveEvent from the button's registered AtkEvent) — needs live verify.
/// </summary>
internal static class HuntPfAgent
{
	/// <summary>
	/// Show agent (if needed), set World + The Hunt tabs, request listings.
	/// </summary>
	public static unsafe bool TryRequestHuntListings()
	{
		try
		{
			var agent = AgentLookingForGroup.Instance();
			if (agent == null)
				return false;

			if (!agent->IsAgentActive())
				agent->Show();

			agent->SearchAreaTab = HuntPfMatch.WorldSearchAreaTab;
			agent->CategoryTab = HuntPfMatch.HuntCategoryTab;
			return agent->RequestCategoryListings(HuntPfMatch.HuntCategoryTab);
		}
		catch
		{
			return false;
		}
	}

	/// <summary>Open a listing's detail pane by server listing id.</summary>
	public static unsafe bool TryOpenListing(ulong listingId)
	{
		if (listingId == 0)
			return false;

		try
		{
			var agent = AgentLookingForGroup.Instance();
			if (agent == null)
				return false;

			if (!agent->IsAgentActive())
				agent->Show();

			return agent->OpenListing(listingId);
		}
		catch
		{
			return false;
		}
	}

	/// <summary>
	/// True when LookingForGroupDetail at <paramref name="addonPtr"/> is ready to join.
	/// </summary>
	public static unsafe bool IsDetailReadyToJoin(nint addonPtr)
	{
		if (addonPtr == nint.Zero)
			return false;

		try
		{
			var addon = (AddonLookingForGroupDetail*)addonPtr;
			if (!addon->IsVisible || !addon->IsReady)
				return false;

			var join = addon->JoinPartyButton;
			return join != null && join->IsEnabled;
		}
		catch
		{
			return false;
		}
	}

	/// <summary>Click Join Party when the detail addon is ready.</summary>
	public static unsafe bool TryClickJoin(nint addonPtr)
	{
		if (addonPtr == nint.Zero)
			return false;

		try
		{
			var addon = (AddonLookingForGroupDetail*)addonPtr;
			if (!addon->IsVisible || !addon->IsReady)
				return false;

			var join = addon->JoinPartyButton;
			if (join == null || !join->IsEnabled)
				return false;

			return ClickAddonButton(join, (AtkUnitBase*)addon);
		}
		catch
		{
			return false;
		}
	}

	/// <summary>
	/// ECommons-style button click: fire the button node's registered AtkEvent via the addon.
	/// </summary>
	private static unsafe bool ClickAddonButton(AtkComponentButton* button, AtkUnitBase* addon)
	{
		if (button == null || addon == null)
			return false;

		var owner = button->AtkComponentBase.OwnerNode;
		if (owner == null)
			return false;

		var evt = owner->AtkResNode.AtkEventManager.Event;
		if (evt == null)
			return false;

		addon->ReceiveEvent(evt->State.EventType, (int)evt->Param, evt);
		return true;
	}
}
