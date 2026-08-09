#nullable enable
using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Gui.PartyFinder.Types;
using Dalamud.Plugin.Services;
using HuntTrainAuto.Logging;

namespace HuntTrainAuto.PartyFinder;

/// <summary>
/// Framework wiring for hunt PF auto-join after flag arrival.
/// Caches <see cref="IPartyFinderGui.ReceiveListing"/>; soft-fails agent/UI join.
/// </summary>
public sealed class HuntPfHelper : IDisposable
{
	private readonly IPartyFinderGui partyFinderGui;
	private readonly IPartyList partyList;
	private readonly ICondition condition;
	private readonly IGameGui gameGui;
	private readonly IPluginLog pluginLog;
	private readonly Func<bool> isEnabled;
	private readonly Func<int> getRetryIntervalMs;
	private readonly Func<bool> isDebugEnabled;

	private readonly object cacheLock = new();
	private readonly Dictionary<ulong, HuntPfListingInfo> listings = new();

	private bool joinedLatch;
	private long nextActionMs;
	private ulong openedListingId;
	private bool subscribed;
	/// <summary>True while Tick is actively seeking a join (gates ReceiveListing cache).</summary>
	private bool seeking;


	public HuntPfHelper(
		IPartyFinderGui partyFinderGui,
		IPartyList partyList,
		ICondition condition,
		IGameGui gameGui,
		IPluginLog pluginLog,
		Func<bool> isEnabled,
		Func<int> getRetryIntervalMs,
		Func<bool>? isDebugEnabled = null)
	{
		this.partyFinderGui = partyFinderGui ?? throw new ArgumentNullException(nameof(partyFinderGui));
		this.partyList = partyList ?? throw new ArgumentNullException(nameof(partyList));
		this.condition = condition ?? throw new ArgumentNullException(nameof(condition));
		this.gameGui = gameGui ?? throw new ArgumentNullException(nameof(gameGui));
		this.pluginLog = pluginLog ?? throw new ArgumentNullException(nameof(pluginLog));
		this.isEnabled = isEnabled ?? throw new ArgumentNullException(nameof(isEnabled));
		this.getRetryIntervalMs = getRetryIntervalMs ?? throw new ArgumentNullException(nameof(getRetryIntervalMs));
		this.isDebugEnabled = isDebugEnabled ?? (() => false);

		try
		{
			this.partyFinderGui.ReceiveListing += OnReceiveListing;
			subscribed = true;
		}
		catch (Exception ex)
		{
			LogDebug($"subscribe soft-fail: {ex.Message}");
		}
	}

	/// <summary>True after observing an in-party join for the current flag leg.</summary>
	public bool JoinedLatch => joinedLatch;

	public void Dispose()
	{
		if (!subscribed)
			return;
		try
		{
			partyFinderGui.ReceiveListing -= OnReceiveListing;
		}
		catch
		{
			// soft-fail
		}

		subscribed = false;
	}

	/// <summary>Reset latch + cache (new flag / territory / master off / dispose).</summary>
	public void Clear()
	{
		joinedLatch = false;
		nextActionMs = 0;
		openedListingId = 0;
		seeking = false;
		lock (cacheLock)
			listings.Clear();
	}

	/// <summary>
	/// One Framework tick while at hunt start. Soft-fails; never throws to Framework.
	/// </summary>
	public void Tick(bool atHuntStart, long nowMs, bool inCombat = false)
	{
		try
		{
			TickCore(atHuntStart, nowMs, inCombat);
		}
		catch (Exception ex)
		{
			LogDebug($"tick soft-fail: {ex.Message}");
		}
	}

	private void TickCore(bool atHuntStart, long nowMs, bool inCombat)
	{
		var enabled = false;
		try
		{
			enabled = isEnabled();
		}
		catch
		{
			enabled = false;
		}

		if (!enabled || !atHuntStart || inCombat)
		{
			seeking = false;
			LogDebugThrottled(
				nowMs,
				$"join suppressed: enabled={enabled}, at-hunt-start={atHuntStart}, in-combat={inCombat}");
			return;
		}

		seeking = true;

		var inParty = IsInParty();
		var wasJoined = joinedLatch;
		joinedLatch = HuntPfDecision.NextJoinedLatch(inParty, joinedLatch);
		if (joinedLatch)
		{
			if (!wasJoined)
				pluginLog.Information("Hunt PF: joined party (latch)");
			seeking = false;
			return;
		}

		// Throttle before addon walks / cache scans — ReadyForGroundFollow can last minutes.
		if (!HuntPfDecision.IsActionReady(nowMs, nextActionMs))
		{
			LogDebugThrottled(nowMs, "join suppressed: action throttle pending");
			return;
		}

		var best = PickBestCached();
		var hasListing = best is not null;
		// Only treat detail as ours when we opened it, it is still best, and agent detail matches.
		var pluginOpened = openedListingId != 0
			&& best is not null
			&& best.Value.Id == openedListingId
			&& HuntPfAgent.IsCurrentDetailListing(openedListingId, pluginLog, IsDebugEnabled());
		var detailReady = pluginOpened
			&& HuntPfAgent.IsDetailReadyToJoin(
				GetDetailAddonPtr(),
				openedListingId,
				pluginLog,
				IsDebugEnabled());
		var kind = HuntPfDecision.Decide(
			enabled,
			atHuntStart,
			inCombat,
			inParty,
			joinedLatch,
			hasListing,
			detailReady,
			pluginOpened,
			actionReady: true);

		switch (kind)
		{
			case HuntPfKind.RefreshListings:
				lock (cacheLock)
					listings.Clear();
				openedListingId = 0;
				var refreshed = HuntPfAgent.TryRequestHuntListings(pluginLog, IsDebugEnabled());
				nextActionMs = HuntPfDecision.NextActionAt(nowMs, getRetryIntervalMs());
				LogDebug(refreshed
					? $"requested The Hunt listings ({HuntPfDecision.Describe(kind)})"
					: "RequestCategoryListings soft-fail; will retry");
				break;

			case HuntPfKind.OpenListing:
				if (best is null)
					break;
				// First open → short settle for ClickJoin. If we come back for the same id,
				// detail never became joinable — drop it so RefreshListings can run (Decide
				// never refreshes while hasSuitableListing stays true). Avoids 750ms OpenListing thrash.
				if (openedListingId == best.Value.Id)
				{
					EvictListing(openedListingId);
					openedListingId = 0;
					nextActionMs = HuntPfDecision.NextActionAt(nowMs, getRetryIntervalMs());
					LogDebug($"drop listing id={best.Value.Id} (detail not ready); will refresh");
					break;
				}

				var opened = HuntPfAgent.TryOpenListing(best.Value.Id, pluginLog, IsDebugEnabled());
				if (opened)
				{
					openedListingId = best.Value.Id;
					nextActionMs = HuntPfDecision.NextOpenSettleAt(
						nowMs,
						HuntPfDecision.DefaultOpenSettleMs);
					LogDebug($"OpenListing id={best.Value.Id} ({HuntPfDecision.Describe(kind)})");
				}
				else
				{
					nextActionMs = HuntPfDecision.NextActionAt(nowMs, getRetryIntervalMs());
					LogDebug("OpenListing soft-fail; will retry");
				}

				break;

			case HuntPfKind.ClickJoin:
				var clickId = openedListingId;
				var clicked = HuntPfAgent.TryClickJoin(GetDetailAddonPtr(), clickId, pluginLog, IsDebugEnabled());
				nextActionMs = HuntPfDecision.NextActionAt(nowMs, getRetryIntervalMs());
				if (clicked)
				{
					DebugBehavior.Info(pluginLog, "PF", $"ClickJoin listing={clickId}");
				}
				else
				{
					LogDebug("ClickJoin soft-fail; will retry");
					// Drop listing so Decide can RefreshListings (hasSuitableListing otherwise traps).
					if (openedListingId != 0)
					{
						EvictListing(openedListingId);
						openedListingId = 0;
					}
				}

				break;
		}
	}

	private void EvictListing(ulong listingId)
	{
		if (listingId == 0)
			return;
		lock (cacheLock)
			listings.Remove(listingId);
	}

	private void OnReceiveListing(IPartyFinderListing listing, IPartyFinderListingEventArgs args)
	{
		try
		{
			// Ignore browse noise unless we are actively seeking a hunt PF join.
			if (!seeking)
				return;

			var info = ToInfo(listing);
			if (!HuntPfMatch.IsSuitable(in info))
				return;

			lock (cacheLock)
				listings[info.Id] = info;
		}
		catch (Exception ex)
		{
			LogDebug($"ReceiveListing soft-fail: {ex.Message}");
		}
	}

	private HuntPfListingInfo? PickBestCached()
	{
		lock (cacheLock)
		{
			if (listings.Count == 0)
				return null;

			// Inline PickBest to avoid per-attempt List alloc on the Framework thread.
			HuntPfListingInfo? best = null;
			var bestScore = int.MinValue;
			foreach (var kv in listings)
			{
				var listing = kv.Value;
				var score = HuntPfMatch.Score(in listing);
				if (score < 0)
					continue;
				if (best is null || score > bestScore || (score == bestScore && listing.Id < best.Value.Id))
				{
					best = listing;
					bestScore = score;
				}
			}

			return best;
		}
	}

	private bool IsInParty()
	{
		try
		{
			if (partyList.Length > 1)
				return true;
			if (condition[ConditionFlag.ParticipatingInCrossWorldPartyOrAlliance])
				return true;
		}
		catch
		{
			// soft-fail
		}

		return false;
	}

	private nint GetDetailAddonPtr()
	{
		try
		{
			var addon = gameGui.GetAddonByName("LookingForGroupDetail");
			return addon.IsNull ? nint.Zero : addon.Address;
		}
		catch
		{
			return nint.Zero;
		}
	}

	private static HuntPfListingInfo ToInfo(IPartyFinderListing listing)
	{
		var desc = string.Empty;
		var name = string.Empty;
		try
		{
			desc = listing.Description.TextValue ?? string.Empty;
		}
		catch
		{
			// soft-fail
		}

		try
		{
			name = listing.Name.TextValue ?? string.Empty;
		}
		catch
		{
			// soft-fail
		}

		return new HuntPfListingInfo
		{
			Id = listing.Id,
			CategoryFlags = (uint)listing.Category,
			SlotsFilled = listing.SlotsFilled,
			SlotsAvailable = listing.SlotsAvailable,
			SearchAreaFlags = (byte)listing.SearchArea,
			Description = desc,
			LeaderName = name,
		};
	}

	private bool IsDebugEnabled()
	{
		try
		{
			return isDebugEnabled();
		}
		catch
		{
			return false;
		}
	}

	private void LogDebug(string message)
		=> DebugBehavior.Debug(pluginLog, IsDebugEnabled(), "PF", message);

	private void LogDebugThrottled(long nowMs, string message)
		=> DebugBehavior.DebugThrottled(
			pluginLog,
			IsDebugEnabled(),
			"pf.join-suppressed",
			HuntPfDecision.DefaultRetryIntervalMs,
			nowMs,
			"PF",
			message);
}
