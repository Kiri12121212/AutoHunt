#nullable enable
using System;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using HuntTrainAuto.Services;

namespace HuntTrainAuto;

/// <summary>
/// Framework-tick TaskUnmount (inverse of <see cref="MountRunner"/>).
/// Soft-fails; never throws to Framework.
/// </summary>
public sealed class UnmountRunner
{
	private readonly UnmountSession session = new();
	private readonly VNavmeshIpc vnav;
	private readonly IObjectTable objectTable;
	private readonly ICondition condition;
	private readonly IPluginLog pluginLog;
	private readonly Func<bool> isTeleportPlanActive;
	private readonly Func<bool> isInstanceChangeActive;
	private bool enqueuedForCurrentArrival;

	public UnmountRunner(
		VNavmeshIpc vnav,
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
		if (!UnmountDecision.ShouldEnqueueIfEnabled(autoUnmountAtFlag))
			return;

		enqueuedForCurrentArrival = true;
		session.Enqueue(Environment.TickCount64);
		pluginLog.Debug("Unmount job enqueued");
	}

	/// <summary>
	/// Enqueue once when <paramref name="isArrived"/> and config allows.
	/// Latches so Framework arrival ticks do not re-enqueue until <see cref="ClearArrivalLatch"/> / new flag.
	/// </summary>
	public void EnqueueOnArrivalIfEnabled(bool autoUnmountAtFlag, bool isArrived)
	{
		if (!UnmountDecision.ShouldEnqueueOnArrival(
			autoUnmountAtFlag,
			isArrived,
			enqueuedForCurrentArrival || session.IsActive))
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
			pluginLog.Debug($"UnmountRunner soft-fail: {ex.Message}");
			session.Clear();
		}
	}

	private void TickCore(bool pathStoppedForArrival, bool arrivalSignaled)
	{
		var now = Environment.TickCount64;
		if (UnmountDecision.IsSessionTimedOut(session.DeadlineMs, now))
		{
			pluginLog.Debug($"Unmount job timed out after {UnmountDecision.SessionTimeoutMs}ms");
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
			return;

		if (session.Phase == UnmountPhase.WaitReady)
			session.EnterUnmounting(now);

		TickUnmounting(now);
	}

	private void TickUnmounting(long now)
	{
		var mounted = condition[ConditionFlag.Mounted];
		if (UnmountDecision.IsUnmountCompleteOrSkipped(mounted))
		{
			session.MarkGroundFollowReady();
			session.Clear();
			pluginLog.Debug("Unmount complete; ready for ground follow (canFly: false)");
			return;
		}

		var checkReady = UnmountDecision.IsCheckReady(session.NextCheckMs, now);
		var dismountReady = now >= session.NextDismountMs;

		var decision = UnmountDecision.DecideUnmountTick(
			mounted,
			condition[ConditionFlag.MountOrOrnamentTransition],
			condition[ConditionFlag.Casting],
			checkReady,
			UnmountDecision.IsDismountActionUsable(GetDismountActionStatus()),
			IsAnimationLocked(),
			dismountReady);

		if (decision.ForceCheckThrottle)
			session.NextCheckMs = UnmountDecision.ForceCheckThrottle(session.NextCheckMs, now);

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
			if (am != null
				&& am->UseAction(ActionType.GeneralAction, UnmountDecision.DismountGeneralActionId))
				return;
		}
		catch (Exception ex)
		{
			pluginLog.Debug($"UseAction dismount soft-fail: {ex.Message}");
		}

		GameChat.TryExecuteCommand("/dismount");
	}
}
