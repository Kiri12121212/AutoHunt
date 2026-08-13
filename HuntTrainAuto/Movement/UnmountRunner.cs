#nullable enable
using System;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using HuntTrainAuto.Logging;

namespace HuntTrainAuto.Movement;

/// <summary>
/// Framework-tick TaskUnmount (inverse of <see cref="MountRunner"/>).
/// Soft-fails; never throws to Framework.
/// </summary>
public sealed class UnmountRunner
{
	private readonly UnmountSession session = new();
	private readonly IVnavmeshService vnav;
	private readonly IObjectTable objectTable;
	private readonly ICondition condition;
	private readonly IPluginLog pluginLog;
	private readonly Func<bool> isTeleportPlanActive;
	private readonly Func<bool> isInstanceChangeActive;
	private bool enqueuedForCurrentArrival;

	public UnmountRunner(
		IVnavmeshService vnav,
		IObjectTable objectTable,
		ICondition condition,
		IPluginLog pluginLog,
		Func<bool> isTeleportPlanActive,
		Func<bool> isInstanceChangeActive)
	{
		this.vnav = vnav;
		this.objectTable = objectTable;
		this.condition = condition;
		this.pluginLog = pluginLog;
		this.isTeleportPlanActive = isTeleportPlanActive;
		this.isInstanceChangeActive = isInstanceChangeActive;
	}

	public UnmountSession Session => session;

	public bool IsActive => session.IsActive;

	/// <summary>
	/// True after successful unmount / already-unmounted skip.
	/// Subsequent follow nav should use <see cref="UnmountDecision.PreferCanFlyForGroundFollow"/>.
	/// </summary>
	public bool ReadyForGroundFollow => session.ReadyForGroundFollow;

	/// <summary>Preferred <c>canFly</c> for follow after unmount success.</summary>
	public bool PreferCanFlyForFollow
		=> ReadyForGroundFollow
			? UnmountDecision.PreferCanFlyForGroundFollow
			: true;

	/// <summary>HTA-style enqueue gated by <paramref name="autoUnmountAtFlag"/>.</summary>
	public void EnqueueIfEnabled(bool autoUnmountAtFlag)
	{
		if (!UnmountDecision.ShouldStartUnmountJob(autoUnmountAtFlag, session.IsActive))
			return;

		enqueuedForCurrentArrival = true;
		session.Enqueue(Environment.TickCount64);
		DebugBehavior.Info(pluginLog, "Unmount", "job enqueued");
	}

	/// <summary>
	/// Enqueue once when <paramref name="isArrived"/> and config allows.
	/// Latches so Framework arrival ticks do not re-enqueue until <see cref="ClearArrivalLatch"/> / new flag.
	/// </summary>
	public void EnqueueOnArrivalIfEnabled(bool autoUnmountAtFlag, bool isArrived, bool huntTargetFound)
	{
		if (!UnmountDecision.ShouldEnqueueOnArrival(
			autoUnmountAtFlag,
			isArrived,
			enqueuedForCurrentArrival || session.IsActive,
			huntTargetFound))
			return;

		EnqueueIfEnabled(autoUnmountAtFlag);
	}

	/// <summary>Clear unmount job only (keeps ground-follow ready).</summary>
	public void Clear() => session.Clear();

	/// <summary>Clear job + arrival enqueue latch + ground-follow (new flag / leave territory).</summary>
	public void ClearAll()
	{
		enqueuedForCurrentArrival = false;
		session.ClearAll();
	}

	/// <summary>Reset one-shot arrival enqueue latch without clearing ground-follow.</summary>
	public void ClearArrivalLatch() => enqueuedForCurrentArrival = false;

	public void Tick(bool pathStoppedForArrival, bool arrivalSignaled)
	{
		if (!session.IsActive)
			return;

		try
		{
			TickCore(pathStoppedForArrival, arrivalSignaled);
		}
		catch (Exception ex)
		{
			DebugBehavior.Debug(pluginLog, enabled: true, "Unmount", $"soft-fail: {ex.Message}");
			session.Clear();
		}
	}

	private void TickCore(bool pathStoppedForArrival, bool arrivalSignaled)
	{
		var now = Environment.TickCount64;
		if (UnmountDecision.IsSessionTimedOut(session.DeadlineMs, now))
		{
			var timeoutMs = session.Phase == UnmountPhase.WaitReady
				? UnmountDecision.WaitReadyTimeoutMs
				: UnmountDecision.SessionTimeoutMs;
			DebugBehavior.Debug(
				pluginLog,
				enabled: true,
				"Unmount",
				$"job timed out after {timeoutMs}ms (phase={session.Phase})");
			// Drop latch so a still-arrived / still-mounted player can re-enqueue.
			enqueuedForCurrentArrival = false;
			session.Clear();
			return;
		}

		var player = objectTable.LocalPlayer;
		var playerReady = TeleportGate.IsPlayerReady(
			player != null,
			player is { CurrentHp: > 0 },
			condition[ConditionFlag.Unconscious]);
		var screenReady = TeleportGate.IsScreenReady(
			condition[ConditionFlag.BetweenAreas],
			condition[ConditionFlag.BetweenAreas51],
			condition[ConditionFlag.OccupiedInCutSceneEvent],
			condition[ConditionFlag.WatchingCutscene]);

		var pathReady = UnmountDecision.IsPathReadyForUnmount(
			vnav.PathIsRunning(),
			arrivalSignaled,
			pathStoppedForArrival);

		if (!UnmountDecision.CanBeginUnmountAttempt(
			pathReady,
			screenReady,
			playerReady,
			isTeleportPlanActive(),
			isInstanceChangeActive()))
		{
			if (DebugThrottle.Try("unmount.waitReady", 2000, now))
			{
				DebugBehavior.Debug(
					pluginLog,
					enabled: true,
					"Unmount",
					$"WaitReady: pathReady={pathReady} pathRunning={vnav.PathIsRunning()} "
					+ $"arrival={arrivalSignaled} pathStop={pathStoppedForArrival} "
					+ $"screen={screenReady} player={playerReady} "
					+ $"tp(ignored)={isTeleportPlanActive()} inst={isInstanceChangeActive()}");
			}

			return;
		}

		if (session.Phase == UnmountPhase.WaitReady)
		{
			session.EnterUnmounting(now);
			DebugBehavior.Debug(pluginLog, enabled: true, "Unmount", "WaitReady → Unmounting");
		}

		TickUnmounting(now);
	}

	private void TickUnmounting(long now)
	{
		var mounted = condition[ConditionFlag.Mounted];
		var inFlight = condition[ConditionFlag.InFlight];
		if (UnmountDecision.IsUnmountCompleteOrSkipped(mounted, inFlight))
		{
			session.MarkGroundFollowReady();
			session.Clear();
			DebugBehavior.Info(pluginLog, "Unmount", "complete; ready for ground follow (canFly: false)");
			return;
		}

		var checkReady = UnmountDecision.IsCheckReady(session.NextCheckMs, now);
		var dismountReady = now >= session.NextDismountMs;
		var actionStatus = GetDismountActionStatus();
		var actionUsable = UnmountDecision.IsDismountActionUsable(actionStatus);
		var animLock = IsAnimationLocked();

		var decision = UnmountDecision.DecideUnmountTick(
			mounted,
			condition[ConditionFlag.MountOrOrnamentTransition],
			condition[ConditionFlag.Casting],
			checkReady,
			actionUsable,
			animLock,
			dismountReady,
			inFlight);

		if (decision.ForceCheckThrottle)
		{
			var cooldown = decision.ForceCheckCooldownMs > 0
				? decision.ForceCheckCooldownMs
				: UnmountDecision.CheckUnmountCooldownMs;
			session.NextCheckMs = UnmountDecision.ForceCheckThrottle(session.NextCheckMs, now, cooldown);
		}

		if (decision.Kind == UnmountTickKind.Wait
		    && DebugThrottle.Try("unmount.wait", 2000, now))
		{
			DebugBehavior.Debug(
				pluginLog,
				enabled: true,
				"Unmount",
				$"{UnmountDecision.Describe(decision)}: usable={actionUsable} status={actionStatus}"
				+ (actionStatus == 579 ? " (Cannot execute / wrong action?)" : string.Empty)
				+ $" checkReady={checkReady} dismountReady={dismountReady} animLock={animLock} "
				+ $"transition={condition[ConditionFlag.MountOrOrnamentTransition]} "
				+ $"casting={condition[ConditionFlag.Casting]} ga={UnmountDecision.DismountGeneralActionId}");
		}

		switch (decision.Kind)
		{
			case UnmountTickKind.Done:
				if (decision.ReadyForGroundFollow)
					session.MarkGroundFollowReady();
				session.Clear();
				return;
			case UnmountTickKind.Wait:
				return;
			case UnmountTickKind.Dismount:
			{
				var next = session.NextDismountMs;
				if (!UnmountDecision.TryFireDismount(ref next, now))
					return;
				session.NextDismountMs = next;
				DebugBehavior.Debug(
					pluginLog,
					enabled: true,
					"Unmount",
					$"{UnmountDecision.Describe(decision)} via UseAction (status was {actionStatus})");
				TryDismount();
				return;
			}
		}
	}

	private unsafe uint GetDismountActionStatus()
	{
		try
		{
			var am = ActionManager.Instance();
			if (am == null)
				return uint.MaxValue;

			return am->GetActionStatus(ActionType.GeneralAction, UnmountDecision.DismountGeneralActionId);
		}
		catch
		{
			return uint.MaxValue;
		}
	}

	private unsafe bool IsAnimationLocked()
	{
		try
		{
			var am = ActionManager.Instance();
			if (am == null)
				return true;

			return am->AnimationLock > 0;
		}
		catch
		{
			return true;
		}
	}

	private unsafe void TryDismount()
	{
		try
		{
			var am = ActionManager.Instance();
			if (am == null)
				return;

			// Re-check status — do not UseAction / chat when unusable (game error spam).
			if (am->GetActionStatus(ActionType.GeneralAction, UnmountDecision.DismountGeneralActionId) != 0)
				return;

			_ = am->UseAction(ActionType.GeneralAction, UnmountDecision.DismountGeneralActionId);
		}
		catch (Exception ex)
		{
			DebugBehavior.Debug(pluginLog, enabled: true, "Unmount", $"UseAction soft-fail: {ex.Message}");
		}
	}
}
