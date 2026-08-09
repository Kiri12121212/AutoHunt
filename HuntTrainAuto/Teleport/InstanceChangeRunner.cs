#nullable enable
using System;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using HuntTrainAuto.Logging;

namespace HuntTrainAuto.Teleport;

/// <summary>
/// Framework-tick port of HTA <c>TaskChangeInstanceAfterTeleport</c> (no ECommons TaskManager).
/// </summary>
public sealed class InstanceChangeRunner
{
	private readonly InstanceChangeSession session = new();
	private readonly ILifestreamService lifestream;
	private readonly IChatOutput chat;
	private readonly IClientState clientState;
	private readonly IObjectTable objectTable;
	private readonly ITargetManager targetManager;
	private readonly ICondition condition;
	private readonly IPluginLog pluginLog;
	private readonly Func<bool> debugEnabled;

	public InstanceChangeRunner(
		ILifestreamService lifestream,
		IChatOutput chat,
		IClientState clientState,
		IObjectTable objectTable,
		ITargetManager targetManager,
		ICondition condition,
		IPluginLog pluginLog,
		Func<bool>? debugEnabled = null)
	{
		this.lifestream = lifestream;
		this.chat = chat;
		this.clientState = clientState;
		this.objectTable = objectTable;
		this.targetManager = targetManager;
		this.condition = condition;
		this.pluginLog = pluginLog;
		this.debugEnabled = debugEnabled ?? (() => false);
	}

	public InstanceChangeSession Session => session;

	public bool IsActive => session.IsActive;

	public void Enqueue(int instance, uint territory)
	{
		if (!InstanceChangeDecision.ShouldEnqueue(instance))
		{
			DebugBehavior.Debug(
				pluginLog,
				debugEnabled(),
				"Instance",
				$"enqueue skipped: requested={instance}");
			return;
		}

		// Replacing a mid-Approach job must stop automove (session.Enqueue clears the flag only).
		if (session.AutomoveStarted)
			StopAutomove();

		session.Enqueue(instance, territory);
		DebugBehavior.Info(pluginLog, "Instance", $"enqueued: requested={instance}, territory={territory}");
	}

	public void Tick()
	{
		if (!session.IsActive)
			return;

		var player = objectTable.LocalPlayer;
		var playerReady = TeleportGate.IsPlayerReady(
			player != null,
			player is { CurrentHp: > 0 },
			condition[ConditionFlag.Unconscious]);
		var betweenAreas = TeleportGate.IsBetweenAreas(
			condition[ConditionFlag.BetweenAreas],
			condition[ConditionFlag.BetweenAreas51]);
		var territoryMatches = clientState.TerritoryType == session.Territory;
		var now = Environment.TickCount64;

		switch (session.Phase)
		{
			case InstanceChangePhase.WaitLanded:
				TickWaitLanded(territoryMatches, playerReady, betweenAreas);
				break;
			case InstanceChangePhase.Approach:
				TickApproach(player, playerReady, now);
				break;
			case InstanceChangePhase.Changing:
				TickChanging(now);
				break;
		}
	}

	public void Clear(bool stopAutomove = true)
	{
		if (stopAutomove && session.AutomoveStarted)
			StopAutomove();
		DebugBehavior.Debug(
			pluginLog,
			debugEnabled(),
			"Instance",
			$"cleared: requested={session.Instance}, phase={session.Phase}");
		session.Clear();
	}

	private void TickWaitLanded(bool territoryMatches, bool playerReady, bool betweenAreas)
	{
		if (!InstanceChangeDecision.IsLanded(territoryMatches, playerReady, betweenAreas))
		{
			DebugBehavior.DebugThrottled(
				pluginLog,
				debugEnabled(),
				"instance.wait-landed",
				1000,
				Environment.TickCount64,
				"Instance",
				$"waiting to land: territoryMatches={territoryMatches}, playerReady={playerReady}, betweenAreas={betweenAreas}");
			return;
		}

		var requested = session.Instance;
		var current = InstanceChangeDecision.ResolveCurrentInstance(
			PublicInstanceReader.TryReadInstanceId(pluginLog, debugEnabled()),
			lifestream.GetCurrentInstance(),
			(int)clientState.Instance);
		var count = lifestream.GetNumberOfInstances();

		if (!InstanceChangeDecision.NeedsInstanceChange(requested, current, count))
		{
			DebugBehavior.Info(
				pluginLog,
				"Instance",
				$"no-op: requested={requested}, current={current}, instances={count}");
			session.Clear();
			return;
		}

		session.EnterApproach();
		DebugBehavior.Info(
			pluginLog,
			"Instance",
			$"landed; approaching aetheryte: requested={requested}, current={current}");
	}

	private void TickApproach(IGameObject? player, bool playerReady, long now)
	{
		var screenReady = TeleportGate.IsScreenReady(
			condition[ConditionFlag.BetweenAreas],
			condition[ConditionFlag.BetweenAreas51],
			condition[ConditionFlag.OccupiedInCutSceneEvent],
			condition[ConditionFlag.WatchingCutscene]);

		if (!screenReady || !playerReady)
		{
			DebugBehavior.DebugThrottled(
				pluginLog,
				debugEnabled(),
				"instance.approach-ready",
				1000,
				now,
				"Instance",
				$"approach waiting: screenReady={screenReady}, playerReady={playerReady}");
			return;
		}

		var current = InstanceChangeDecision.ResolveCurrentInstance(
			PublicInstanceReader.TryReadInstanceId(pluginLog, debugEnabled()),
			lifestream.GetCurrentInstance(),
			(int)clientState.Instance);
		if (InstanceChangeDecision.IsAlreadyOnInstance(session.Instance, current))
		{
			FinishSuccess();
			return;
		}

		var canChange = lifestream.CanChangeInstance();
		var nearest = FindNearestTargetableAetheryte(player);
		var targeted = nearest != null && IsTarget(nearest);

		var action = InstanceChangeDecision.DecideApproach(
			canChange,
			nearest != null,
			targeted,
			now >= session.NextTargetMs,
			now >= session.NextLockonMs);
		if (action == InstanceChangeDecision.ApproachAction.Wait)
		{
			DebugBehavior.DebugThrottled(
				pluginLog,
				debugEnabled(),
				"instance.approach",
				1000,
				now,
				"Instance",
				$"{InstanceChangeDecision.Describe(action)}: requested={session.Instance}, current={current}, canChange={canChange}, targeted={targeted}");
		}
		else
		{
			DebugBehavior.Debug(
				pluginLog,
				debugEnabled(),
				"Instance",
				$"{InstanceChangeDecision.Describe(action)}: requested={session.Instance}, current={current}, canChange={canChange}, targeted={targeted}");
		}

		switch (action)
		{
			case InstanceChangeDecision.ApproachAction.Wait:
				return;
			case InstanceChangeDecision.ApproachAction.SetTarget:
				targetManager.Target = nearest;
				session.NextTargetMs = now + InstanceChangeDecision.TargetThrottleMs;
				DebugBehavior.Debug(pluginLog, debugEnabled(), "Instance", "aetheryte targeted");
				return;
			case InstanceChangeDecision.ApproachAction.LockonAndAutomove:
				chat.TryExecuteCommand("/lockon");
				chat.TryExecuteCommand("/automove on");
				session.AutomoveStarted = true;
				session.NextLockonMs = now + InstanceChangeDecision.LockonThrottleMs;
				DebugBehavior.Info(pluginLog, "Instance", "lockon and automove started");
				return;
			case InstanceChangeDecision.ApproachAction.SoftAbortNoAetheryte:
				DebugBehavior.Debug(
					pluginLog,
					debugEnabled(),
					"Instance",
					$"soft-abort: no targetable aetheryte, requested={session.Instance}");
				Clear(stopAutomove: true);
				return;
			case InstanceChangeDecision.ApproachAction.ReadyToChange:
				session.EnterChanging(now);
				DebugBehavior.Info(pluginLog, "Instance", $"ready to change: requested={session.Instance}");
				TickChanging(now);
				return;
		}
	}

	private void TickChanging(long now)
	{
		var current = InstanceChangeDecision.ResolveCurrentInstance(
			PublicInstanceReader.TryReadInstanceId(pluginLog, debugEnabled()),
			lifestream.GetCurrentInstance(),
			(int)clientState.Instance);
		var already = InstanceChangeDecision.IsAlreadyOnInstance(session.Instance, current);
		var timedOut = now >= session.ChangeDeadlineMs;
		var canChange = lifestream.CanChangeInstance();

		var result = InstanceChangeDecision.DecideChangeTick(
			already,
			canChange,
			session.ChangeIssued,
			timedOut);
		DebugBehavior.DebugThrottled(
			pluginLog,
			debugEnabled(),
			"instance.changing",
			1000,
			now,
			"Instance",
			$"{InstanceChangeDecision.Describe(result)}: requested={session.Instance}, current={current}, canChange={canChange}, issued={session.ChangeIssued}");

		switch (result)
		{
			case InstanceChangeDecision.ChangeTickResult.Succeeded:
				FinishSuccess();
				return;
			case InstanceChangeDecision.ChangeTickResult.TimedOut:
				DebugBehavior.Debug(
					pluginLog,
					debugEnabled(),
					$"Instance",
					$"timed out after {InstanceChangeDecision.ChangeTimeoutMs}ms: requested={session.Instance}, current={current}");
				Clear(stopAutomove: true);
				return;
			case InstanceChangeDecision.ChangeTickResult.IssueChange:
				if (session.AutomoveStarted)
					StopAutomove();
				lifestream.ChangeInstance(session.Instance);
				session.ChangeIssued = true;
				DebugBehavior.Info(pluginLog, "Instance", $"issuing change: requested={session.Instance}");
				return;
			case InstanceChangeDecision.ChangeTickResult.Continue:
				return;
		}
	}

	private void FinishSuccess()
	{
		if (session.AutomoveStarted)
			StopAutomove();
		DebugBehavior.Info(pluginLog, "Instance", $"complete: instance={session.Instance}");
		session.Clear();
	}

	private void StopAutomove()
	{
		chat.TryExecuteCommand("/automove off");
		session.AutomoveStarted = false;
		DebugBehavior.Debug(pluginLog, debugEnabled(), "Instance", "automove stopped");
	}

	private IGameObject? FindNearestTargetableAetheryte(IGameObject? player)
	{
		if (player == null)
			return null;

		IGameObject? best = null;
		var bestDist = float.MaxValue;
		var origin = player.Position;

		foreach (var obj in objectTable)
		{
			if (obj.ObjectKind != ObjectKind.Aetheryte || !obj.IsTargetable)
				continue;

			var d = Vector3.DistanceSquared(origin, obj.Position);
			if (d >= bestDist)
				continue;

			bestDist = d;
			best = obj;
		}

		return best;
	}

	private bool IsTarget(IGameObject candidate)
	{
		var target = targetManager.Target;
		if (target == null)
			return false;

		return target.GameObjectId == candidate.GameObjectId;
	}
}
