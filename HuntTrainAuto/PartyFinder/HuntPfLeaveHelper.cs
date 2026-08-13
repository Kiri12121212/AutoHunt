#nullable enable
using System;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using HuntTrainAuto.Contracts;
using HuntTrainAuto.Logging;

namespace HuntTrainAuto.PartyFinder;

/// <summary>
/// Framework wiring for opt-in hunt-party leave at train end.
/// Soft-fails <c>/leave</c>; clears join latch via <see cref="onLeft"/>.
/// </summary>
public sealed class HuntPfLeaveHelper
{
	private readonly IChatOutput chat;
	private readonly IPartyList partyList;
	private readonly ICondition condition;
	private readonly IPluginLog pluginLog;
	private readonly Func<bool> isEnabled;
	private readonly Func<int> getIdleLeaveMs;
	private readonly Func<bool> isSessionActive;
	private readonly Action onLeft;
	private readonly Action onLatchCleared;
	private readonly Func<bool> isDebugEnabled;

	private bool armedLastStop;
	private long lastCombatEndMs;
	private long lastFlagMs;
	private long nextActionMs;

	public HuntPfLeaveHelper(
		IChatOutput chat,
		IPartyList partyList,
		ICondition condition,
		IPluginLog pluginLog,
		Func<bool> isEnabled,
		Func<int> getIdleLeaveMs,
		Func<bool> isSessionActive,
		Action onLeft,
		Func<bool>? isDebugEnabled = null,
		Action? onLatchCleared = null)
	{
		this.chat = chat ?? throw new ArgumentNullException(nameof(chat));
		this.partyList = partyList ?? throw new ArgumentNullException(nameof(partyList));
		this.condition = condition ?? throw new ArgumentNullException(nameof(condition));
		this.pluginLog = pluginLog ?? throw new ArgumentNullException(nameof(pluginLog));
		this.isEnabled = isEnabled ?? throw new ArgumentNullException(nameof(isEnabled));
		this.getIdleLeaveMs = getIdleLeaveMs ?? throw new ArgumentNullException(nameof(getIdleLeaveMs));
		this.isSessionActive = isSessionActive ?? throw new ArgumentNullException(nameof(isSessionActive));
		this.onLeft = onLeft ?? throw new ArgumentNullException(nameof(onLeft));
		this.onLatchCleared = onLatchCleared ?? onLeft;
		this.isDebugEnabled = isDebugEnabled ?? (() => false);
	}

	/// <summary>True after conductor LAST STOP until leave/clear.</summary>
	public bool ArmedLastStop => armedLastStop;

	/// <summary>Arm leave-after-this-combat from conductor LAST STOP chat.</summary>
	public void ArmLastStop()
	{
		armedLastStop = true;
		DebugBehavior.Info(pluginLog, "PF", "LAST STOP armed (leave after this combat)");
	}

	/// <summary>Reset leave state (new flag / territory / master off / dispose).</summary>
	public void Clear()
	{
		armedLastStop = false;
		lastCombatEndMs = 0;
		lastFlagMs = 0;
		nextActionMs = 0;
	}

	/// <summary>Stamp a new flag so idle leave does not fire mid-train.</summary>
	public void NoteFlag(long nowMs)
	{
		lastFlagMs = nowMs;
		// Keep armedLastStop: conductors often shout LAST STOP then post the final flag.
	}

	/// <summary>
	/// One Framework tick after combat. Soft-fails; never throws to Framework.
	/// </summary>
	public void Tick(bool wasInCombat, bool inCombat, long nowMs)
	{
		try
		{
			TickCore(wasInCombat, inCombat, nowMs);
		}
		catch (Exception ex)
		{
			LogDebug($"leave tick soft-fail: {ex.Message}");
		}
	}

	private void TickCore(bool wasInCombat, bool inCombat, long nowMs)
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

		if (!enabled)
		{
			LogDebugThrottled(nowMs, "leave suppressed: disabled");
			return;
		}

		if (HuntPfLeaveDecision.ShouldNoteCombatEnd(wasInCombat, inCombat))
		{
			lastCombatEndMs = nowMs;
			LogDebug("combat end noted");
		}

		var sessionActive = false;
		try
		{
			sessionActive = isSessionActive() || armedLastStop;
		}
		catch
		{
			sessionActive = armedLastStop;
		}

		var inParty = IsInParty();
		var actionReady = HuntPfLeaveDecision.IsActionReady(nowMs, nextActionMs);
		var idleMs = HuntPfLeaveDecision.DefaultIdleLeaveMs;
		try
		{
			idleMs = getIdleLeaveMs();
		}
		catch
		{
			idleMs = HuntPfLeaveDecision.DefaultIdleLeaveMs;
		}

		var kind = HuntPfLeaveDecision.Decide(
			enabled,
			inParty,
			sessionActive,
			armedLastStop,
			wasInCombat,
			inCombat,
			nowMs,
			lastCombatEndMs,
			lastFlagMs,
			idleMs,
			actionReady);
		if (kind == HuntPfLeaveKind.None)
			LogDebugThrottled(
				nowMs,
				$"leave suppressed: session={sessionActive}, party={inParty}, combat={inCombat}, ready={actionReady}");

		switch (kind)
		{
			case HuntPfLeaveKind.ClearLatchOnly:
				DebugBehavior.Info(pluginLog, "PF", $"party gone; clearing latches ({HuntPfLeaveDecision.Describe(kind)})");
				Clear();
				try
				{
					onLatchCleared();
				}
				catch (Exception ex)
				{
					LogDebug($"onLatchCleared soft-fail: {ex.Message}");
				}

				break;

			case HuntPfLeaveKind.LeaveAfterArmedCombatEnd:
			case HuntPfLeaveKind.LeaveIdleTimeout:
				nextActionMs = HuntPfLeaveDecision.NextActionAt(nowMs);
				var ok = chat.TryExecuteCommand("/leave");
				DebugBehavior.Info(
					pluginLog,
					"PF",
					ok
						? $"/leave ({HuntPfLeaveDecision.Describe(kind)})"
						: $"/leave soft-fail ({HuntPfLeaveDecision.Describe(kind)}); will retry");
				if (ok || !IsInParty())
					FinishLeft();
				break;
		}
	}

	private void FinishLeft()
	{
		Clear();
		try
		{
			onLeft();
		}
		catch (Exception ex)
		{
			LogDebug($"onLeft soft-fail: {ex.Message}");
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
			"pf.leave-suppressed",
			HuntPfLeaveDecision.DefaultRetryIntervalMs,
			nowMs,
			"PF",
			message);
}
