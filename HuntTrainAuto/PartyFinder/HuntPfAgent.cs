#nullable enable
using System;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using HuntTrainAuto.Logging;

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
	public static unsafe bool TryRequestHuntListings(IPluginLog? log = null, bool debugEnabled = false)
	{
		try
		{
			var agent = AgentLookingForGroup.Instance();
			if (agent == null)
			{
				Log(log, debugEnabled, "request listings rejected: agent unavailable");
				return false;
			}

			if (!agent->IsAgentActive())
				agent->Show();

			agent->SearchAreaTab = HuntPfMatch.WorldSearchAreaTab;
			agent->CategoryTab = HuntPfMatch.HuntCategoryTab;
			var requested = agent->RequestCategoryListings(HuntPfMatch.HuntCategoryTab);
			if (!requested)
				Log(log, debugEnabled, "request listings rejected by agent");
			return requested;
		}
		catch (Exception ex)
		{
			Log(log, debugEnabled, $"request listings soft-fail: {ex.Message}");
			return false;
		}
	}

	/// <summary>Open a listing's detail pane by server listing id.</summary>
	public static unsafe bool TryOpenListing(
		ulong listingId,
		IPluginLog? log = null,
		bool debugEnabled = false)
	{
		if (listingId == 0)
		{
			Log(log, debugEnabled, "open listing rejected: id=0");
			return false;
		}

		try
		{
			var agent = AgentLookingForGroup.Instance();
			if (agent == null)
			{
				Log(log, debugEnabled, "open listing rejected: agent unavailable");
				return false;
			}

			if (!agent->IsAgentActive())
				agent->Show();

			var opened = agent->OpenListing(listingId);
			if (!opened)
				Log(log, debugEnabled, $"open listing rejected by agent: id={listingId}");
			return opened;
		}
		catch (Exception ex)
		{
			Log(log, debugEnabled, $"open listing soft-fail: {ex.Message}");
			return false;
		}
	}

	/// <summary>
	/// True when LookingForGroupDetail at <paramref name="addonPtr"/> is ready to join
	/// and the agent's last-viewed listing matches <paramref name="expectedListingId"/>.
	/// </summary>
	public static unsafe bool IsDetailReadyToJoin(
		nint addonPtr,
		ulong expectedListingId,
		IPluginLog? log = null,
		bool debugEnabled = false)
	{
		if (addonPtr == nint.Zero || expectedListingId == 0)
		{
			Log(log, debugEnabled, "detail not ready: addon or listing unavailable");
			return false;
		}

		try
		{
			if (!IsCurrentDetailListing(expectedListingId, log, debugEnabled))
				return false;

			var addon = (AddonLookingForGroupDetail*)addonPtr;
			if (!addon->IsVisible || !addon->IsReady)
				return false;

			var join = addon->JoinPartyButton;
			return join != null && join->IsEnabled;
		}
		catch (Exception ex)
		{
			Log(log, debugEnabled, $"detail readiness soft-fail: {ex.Message}");
			return false;
		}
	}

	/// <summary>Click Join Party only when the open detail is still <paramref name="expectedListingId"/>.</summary>
	public static unsafe bool TryClickJoin(
		nint addonPtr,
		ulong expectedListingId,
		IPluginLog? log = null,
		bool debugEnabled = false)
	{
		if (addonPtr == nint.Zero || expectedListingId == 0)
		{
			Log(log, debugEnabled, "click join rejected: addon or listing unavailable");
			return false;
		}

		try
		{
			if (!IsCurrentDetailListing(expectedListingId, log, debugEnabled))
				return false;

			var addon = (AddonLookingForGroupDetail*)addonPtr;
			if (!addon->IsVisible || !addon->IsReady)
				return false;

			var join = addon->JoinPartyButton;
			if (join == null || !join->IsEnabled)
				return false;

			return ClickAddonButton(join, (AtkUnitBase*)addon);
		}
		catch (Exception ex)
		{
			Log(log, debugEnabled, $"click join soft-fail: {ex.Message}");
			return false;
		}
	}

	/// <summary>
	/// True when agent last-viewed detail listing id matches the one we OpenListing'd.
	/// Soft-fails closed (false) if agent unavailable.
	/// </summary>
	public static unsafe bool IsCurrentDetailListing(
		ulong expectedListingId,
		IPluginLog? log = null,
		bool debugEnabled = false)
	{
		if (expectedListingId == 0)
		{
			Log(log, debugEnabled, "detail listing mismatch: expected id=0");
			return false;
		}

		try
		{
			var agent = AgentLookingForGroup.Instance();
			if (agent == null)
			{
				Log(log, debugEnabled, "detail listing mismatch: agent unavailable");
				return false;
			}

			var matches = agent->LastViewedListing.ListingId == expectedListingId;
			if (!matches)
				Log(log, debugEnabled, $"detail listing mismatch: expected={expectedListingId}");
			return matches;
		}
		catch (Exception ex)
		{
			Log(log, debugEnabled, $"detail listing check soft-fail: {ex.Message}");
			return false;
		}
	}

	/// <summary>
	/// True when LookingForGroupDetail at <paramref name="addonPtr"/> is ready to join.
	/// Prefer the overload that binds to an expected listing id.
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

	/// <summary>Click Join Party when the detail addon is ready (no listing-id bind).</summary>
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

	private static void Log(IPluginLog? log, bool debugEnabled, string message)
	{
		if (log != null)
			DebugBehavior.Debug(log, debugEnabled, "PF", message);
	}
}
