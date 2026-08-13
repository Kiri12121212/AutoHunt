#nullable enable

using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using HuntTrainAuto.Chat;
using HuntTrainAuto.Contracts;
using HuntTrainAuto.Logging;
using HuntTrainAuto.Teleport;

namespace HuntTrainAuto.HuntAlerts;

/// <summary>
/// Framework-tick join: ChangeWorld → aetheryte TP → optional instance →
/// <c>/sea first</c> + conductor assign once landed.
/// </summary>
public sealed class HuntJoinRunner
{
	private readonly HuntJoinSession session = new();
	private readonly ILifestreamService lifestream;
	private readonly ITeleporterService teleporter;
	private readonly IChatOutput chat;
	private readonly IClientState clientState;
	private readonly IObjectTable objectTable;
	private readonly ICondition condition;
	private readonly IPluginLog pluginLog;
	private readonly Func<bool> debugEnabled;
	private readonly Func<IList<string>> conductors;
	private readonly Action saveConfig;
	private readonly Func<string?> currentWorldName;
	private readonly Action<int, uint> enqueueInstance;
	private readonly Func<bool> instanceJobActive;
	private readonly Func<int> currentInstance;

	public HuntJoinRunner(
		ILifestreamService lifestream,
		ITeleporterService teleporter,
		IChatOutput chat,
		IClientState clientState,
		IObjectTable objectTable,
		ICondition condition,
		IPluginLog pluginLog,
		Func<bool> debugEnabled,
		Func<IList<string>> conductors,
		Action saveConfig,
		Func<string?> currentWorldName,
		Action<int, uint> enqueueInstance,
		Func<bool> instanceJobActive,
		Func<int> currentInstance)
	{
		this.lifestream = lifestream;
		this.teleporter = teleporter;
		this.chat = chat;
		this.clientState = clientState;
		this.objectTable = objectTable;
		this.condition = condition;
		this.pluginLog = pluginLog;
		this.debugEnabled = debugEnabled;
		this.conductors = conductors;
		this.saveConfig = saveConfig;
		this.currentWorldName = currentWorldName;
		this.enqueueInstance = enqueueInstance;
		this.instanceJobActive = instanceJobActive;
		this.currentInstance = currentInstance;
	}

	public bool IsActive => session.IsActive;

	public string Status => session.Status;

	public string? Start(HuntTrainMessage? message)
	{
		if (!HuntJoinDecision.TryPlan(message, out var plan, out var reject))
			return reject;

		var now = Environment.TickCount64;
		var player = objectTable.LocalPlayer;
		var playerReady = TeleportGate.IsPlayerReady(
			player != null,
			player is { CurrentHp: > 0 },
			condition[ConditionFlag.Unconscious]);
		var betweenAreas = TeleportGate.IsBetweenAreas(
			condition[ConditionFlag.BetweenAreas],
			condition[ConditionFlag.BetweenAreas51]);
		var worldMatches = HuntJoinDecision.IsWorldMatch(currentWorldName(), plan.World);
		var territoryMatches = HuntJoinDecision.IsTerritoryMatch(
			clientState.TerritoryType,
			plan.TerritoryTypeId);
		var landed = HuntJoinDecision.IsLanded(playerReady, betweenAreas);
		var needsInstance = NeedsInstance(plan);
		var phase = HuntJoinDecision.InitialPhase(worldMatches, territoryMatches, landed, needsInstance);

		session.Start(plan, phase, now);
		if (phase == HuntJoinPhase.WaitInstance)
			EnqueueInstance(plan);
		else if (phase == HuntJoinPhase.SearchAssign)
			SearchAndAssign(plan);

		Debug($"start: {HuntJoinDecision.Describe(plan)} phase={phase}");
		return null;
	}

	public void Tick()
	{
		if (!session.IsActive)
			return;

		var plan = session.Plan;
		var now = Environment.TickCount64;
		var player = objectTable.LocalPlayer;
		var playerReady = TeleportGate.IsPlayerReady(
			player != null,
			player is { CurrentHp: > 0 },
			condition[ConditionFlag.Unconscious]);
		var betweenAreas = TeleportGate.IsBetweenAreas(
			condition[ConditionFlag.BetweenAreas],
			condition[ConditionFlag.BetweenAreas51]);
		var worldMatches = HuntJoinDecision.IsWorldMatch(currentWorldName(), plan.World);
		var territoryMatches = HuntJoinDecision.IsTerritoryMatch(
			clientState.TerritoryType,
			plan.TerritoryTypeId);
		var needsInstance = session.Phase == HuntJoinPhase.WaitInstance
			|| NeedsInstance(plan);

		var step = HuntJoinDecision.Decide(
			session.Phase,
			worldMatches,
			territoryMatches,
			betweenAreas,
			playerReady,
			lifestream.IsBusy(),
			instanceJobActive(),
			needsInstance,
			session.IsRetryReady(now),
			session.IsTimedOut(now));

		switch (step)
		{
			case HuntJoinStep.Wait:
				DebugThrottled(
					$"wait phase={session.Phase} world={worldMatches} territory={territoryMatches} between={betweenAreas}");
				return;
			case HuntJoinStep.TimedOut:
				Fail("timed out");
				return;
			case HuntJoinStep.ChangeWorld:
				IssueChangeWorld(plan, now);
				return;
			case HuntJoinStep.AdvanceToTeleport:
				session.SetPhase(HuntJoinPhase.Teleport, now);
				session.Status = $"teleport {plan.PlaceName}";
				IssueTeleport(plan, now);
				return;
			case HuntJoinStep.Teleport:
				IssueTeleport(plan, now);
				return;
			case HuntJoinStep.AdvanceToInstance:
				session.SetPhase(HuntJoinPhase.WaitInstance, now);
				session.Status = $"instance {plan.Instance}";
				EnqueueInstance(plan);
				return;
			case HuntJoinStep.EnqueueInstance:
				EnqueueInstance(plan);
				return;
			case HuntJoinStep.AdvanceToSearch:
			case HuntJoinStep.SearchAndAssign:
				SearchAndAssign(plan);
				return;
		}
	}

	public void Clear()
	{
		if (!session.IsActive && string.IsNullOrEmpty(session.Status))
			return;
		Debug($"cleared phase={session.Phase}");
		session.Clear();
	}

	private bool NeedsInstance(HuntJoinDecision.Plan plan)
	{
		try
		{
			return InstanceChangeDecision.ShouldEnqueueIfNeeded(plan.Instance, currentInstance());
		}
		catch
		{
			return InstanceChangeDecision.ShouldEnqueue(plan.Instance);
		}
	}

	private void IssueChangeWorld(HuntJoinDecision.Plan plan, long now)
	{
		session.SetPhase(HuntJoinPhase.WorldVisit, now, retryNow: false);
		session.MarkRetry(now);
		session.Status = $"world visit {plan.World}";
		var ok = lifestream.ChangeWorld(plan.World);
		Debug($"ChangeWorld {(ok ? "accepted" : "declined")}: {plan.World}");
	}

	private void IssueTeleport(HuntJoinDecision.Plan plan, long now)
	{
		session.MarkRetry(now);
		session.Status = $"teleport {plan.PlaceName}";
		if (teleporter.Teleport(plan.AetheryteId, 0))
		{
			Debug($"teleport accepted: aetheryte={plan.AetheryteId}");
			return;
		}

		if (lifestream.Teleport(plan.AetheryteId))
		{
			Debug($"lifestream teleport accepted: aetheryte={plan.AetheryteId}");
			return;
		}

		Debug($"teleport declined: aetheryte={plan.AetheryteId}");
	}

	private void EnqueueInstance(HuntJoinDecision.Plan plan)
	{
		var territory = plan.TerritoryTypeId != 0
			? plan.TerritoryTypeId
			: clientState.TerritoryType;
		enqueueInstance(plan.Instance, territory);
		session.Status = $"instance {plan.Instance}";
		Debug($"instance enqueued: {plan.Instance} territory={territory}");
	}

	private void SearchAndAssign(HuntJoinDecision.Plan plan)
	{
		var command = HuntJoinDecision.FormatSearchCommand(plan.ConductorName);
		var searched = chat.TryExecuteCommand(command);
		Debug($"{command} {(searched ? "ok" : "soft-fail")}");

		try
		{
			var added = ConductorList.TryAdd(conductors(), plan.ConductorName);
			if (added)
			{
				try
				{
					saveConfig();
				}
				catch (Exception ex)
				{
					Debug($"save soft-fail: {ex.Message}");
				}

				session.Clear();
				session.Status = $"added conductor '{plan.ConductorName}'";
			}
			else
			{
				session.Clear();
				session.Status = $"conductor already present '{plan.ConductorName}'";
			}

			Debug(session.Status);
		}
		catch (Exception ex)
		{
			session.Clear();
			session.Status = $"assign soft-fail: {ex.Message}";
			Debug(session.Status);
		}
	}

	private void Fail(string reason)
	{
		Debug($"join failed: {reason}");
		session.Clear();
		session.Status = $"join failed: {reason}";
	}

	private void Debug(string message)
		=> DebugBehavior.Debug(pluginLog, debugEnabled(), "HuntJoin", message);

	private void DebugThrottled(string message)
		=> DebugBehavior.DebugThrottled(
			pluginLog,
			debugEnabled(),
			"huntjoin.wait",
			1000,
			Environment.TickCount64,
			"HuntJoin",
			message);
}
