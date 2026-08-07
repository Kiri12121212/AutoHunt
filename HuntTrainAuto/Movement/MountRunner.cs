#nullable enable
using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;

namespace HuntTrainAuto.Movement;

/// <summary>
/// Framework-tick port of HTA <c>TaskMount</c> (no ECommons TaskManager).
/// Soft-fails; never throws to Framework.
/// </summary>
public sealed class MountRunner
{
	private readonly MountSession session = new();
	private readonly ILifestreamService lifestream;
	private readonly IChatOutput chat;
	private readonly IObjectTable objectTable;
	private readonly ICondition condition;
	private readonly IDataManager dataManager;
	private readonly IPluginLog pluginLog;
	private readonly Func<bool> isInstanceChangeActive;

	public MountRunner(
		ILifestreamService lifestream,
		IChatOutput chat,
		IObjectTable objectTable,
		ICondition condition,
		IDataManager dataManager,
		IPluginLog pluginLog,
		Func<bool> isInstanceChangeActive)
	{
		this.lifestream = lifestream;
		this.chat = chat;
		this.objectTable = objectTable;
		this.condition = condition;
		this.dataManager = dataManager;
		this.pluginLog = pluginLog;
		this.isInstanceChangeActive = isInstanceChangeActive;
	}

	public MountSession Session => session;

	public bool IsActive => session.IsActive;

	/// <summary>HTA <c>TaskMount.EnqueueIfEnabled</c> — gated by <paramref name="useMount"/>.</summary>
	public void EnqueueIfEnabled(bool useMount)
	{
		if (!MountDecision.ShouldEnqueueIfEnabled(useMount))
			return;

		session.Enqueue(Environment.TickCount64);
		pluginLog.Debug("Mount job enqueued");
	}

	public void Clear() => session.Clear();

	public void Tick(int mountConfig)
	{
		if (!session.IsActive)
			return;

		try
		{
			TickCore(mountConfig);
		}
		catch (Exception ex)
		{
			pluginLog.Debug($"MountRunner soft-fail: {ex.Message}");
			session.Clear();
		}
	}

	private void TickCore(int mountConfig)
	{
		var now = Environment.TickCount64;
		if (MountDecision.IsSessionTimedOut(session.DeadlineMs, now))
		{
			pluginLog.Debug($"Mount job timed out after {MountDecision.SessionTimeoutMs}ms");
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

		if (!MountDecision.CanBeginMountAttempt(
			lifestream.IsBusy(),
			screenReady,
			playerReady,
			isInstanceChangeActive()))
			return;

		if (session.Phase == MountPhase.WaitReady)
			session.EnterMounting(now);

		TickMounting(mountConfig, now);
	}

	private void TickMounting(int mountConfig, long now)
	{
		var mounted = condition[ConditionFlag.Mounted];
		if (MountDecision.IsMountCompleteOrSkipped(mounted, mountConfig))
		{
			session.Clear();
			return;
		}

		var resolve = ResolveMountSelection(mountConfig);
		var checkReady = MountDecision.IsCheckReady(session.NextCheckMs, now);
		var summonReady = now >= session.NextSummonMs;

		var decision = MountDecision.DecideMountTick(
			mounted,
			mountConfig,
			condition[ConditionFlag.MountOrOrnamentTransition],
			condition[ConditionFlag.Casting],
			checkReady,
			MountDecision.IsMountActionUsable(GetMountActionStatus()),
			resolve,
			IsAnimationLocked(),
			summonReady);

		if (decision.ForceCheckThrottle)
			session.NextCheckMs = MountDecision.ForceCheckThrottle(session.NextCheckMs, now);

		EmitWarnings(decision);

		switch (decision.Kind)
		{
			case MountTickKind.Done:
				session.Clear();
				return;
			case MountTickKind.Wait:
				return;
			case MountTickKind.SummonRandom:
			{
				var next = session.NextSummonMs;
				if (!MountDecision.TryFireSummon(ref next, now))
					return;
				session.NextSummonMs = next;
				TrySummonRandom();
				return;
			}
			case MountTickKind.SummonSpecific:
			{
				var next = session.NextSummonMs;
				if (!MountDecision.TryFireSummon(ref next, now))
					return;
				session.NextSummonMs = next;
				TrySummonNamed(decision.SummonMountId);
				return;
			}
		}
	}

	private void EmitWarnings(MountTickResult decision)
	{
		if (decision.WarnNoMounts && !session.WarnedNoMounts)
		{
			session.WarnedNoMounts = true;
			pluginLog.Warning("No unlocked mounts found");
		}

		if (decision.WarnFallback && !session.WarnedFallback)
		{
			session.WarnedFallback = true;
			var requested = GetMountName(decision.RequestedMountId);
			var fallback = decision.FallbackMountId == MountDecision.RandomMount
				? "random mount"
				: GetMountName(decision.FallbackMountId);
			pluginLog.Warning($"Mount {requested} is not unlocked. Selecting {fallback}.");
		}
	}

	private MountResolveResult ResolveMountSelection(int configuredMount)
	{
		if (configuredMount == MountDecision.NeverMount)
		{
			return new MountResolveResult { Kind = MountResolveKind.NoUnlocked, MountId = MountDecision.NeverMount };
		}

		var unlocked = CollectUnlockedMountIds();
		var configuredUnlocked = configuredMount != MountDecision.RandomMount
			&& IsMountUnlocked((uint)configuredMount);

		return MountDecision.ResolveMount(
			configuredMount,
			configuredUnlocked,
			unlocked.Count,
			unlocked.Count > 0 ? unlocked[0] : 0u);
	}

	private List<uint> CollectUnlockedMountIds()
	{
		var result = new List<uint>();
		try
		{
			var sheet = dataManager.GetExcelSheet<Mount>();
			if (sheet == null)
				return result;

			foreach (var row in sheet)
			{
				if (!HasSingular(row.Singular))
					continue;
				if (!IsMountUnlocked(row.RowId))
					continue;
				result.Add(row.RowId);
			}
		}
		catch (Exception ex)
		{
			pluginLog.Debug($"CollectUnlockedMountIds soft-fail: {ex.Message}");
		}

		return result;
	}

	private static bool HasSingular(ReadOnlySeString singular)
	{
		try
		{
			return !string.IsNullOrEmpty(singular.ExtractText());
		}
		catch
		{
			return false;
		}
	}

	private string GetMountName(int mountId)
	{
		if (mountId <= 0)
			return mountId.ToString();

		try
		{
			var row = dataManager.GetExcelSheet<Mount>()?.GetRowOrDefault((uint)mountId);
			if (row == null)
				return mountId.ToString();

			var name = row.Value.Singular.ExtractText();
			return string.IsNullOrEmpty(name) ? mountId.ToString() : name;
		}
		catch
		{
			return mountId.ToString();
		}
	}

	private unsafe uint GetMountActionStatus()
	{
		try
		{
			var am = ActionManager.Instance();
			if (am == null)
				return uint.MaxValue;

			return am->GetActionStatus(ActionType.GeneralAction, MountDecision.MountGeneralActionId);
		}
		catch
		{
			return uint.MaxValue;
		}
	}

	private unsafe bool IsMountUnlocked(uint mountId)
	{
		try
		{
			var state = PlayerState.Instance();
			if (state == null)
				return false;

			return state->IsMountUnlocked(mountId);
		}
		catch
		{
			return false;
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

	private unsafe void TrySummonRandom()
	{
		try
		{
			var am = ActionManager.Instance();
			if (am != null
				&& am->UseAction(ActionType.GeneralAction, MountDecision.MountGeneralActionId))
				return;
		}
		catch (Exception ex)
		{
			pluginLog.Debug($"UseAction mount soft-fail: {ex.Message}");
		}

		chat.TryExecuteCommand("/mount");
	}

	private void TrySummonNamed(int mountId)
	{
		var name = GetMountName(mountId);
		if (string.IsNullOrWhiteSpace(name))
		{
			TrySummonRandom();
			return;
		}

		chat.TryExecuteCommand($"/mount \"{name}\"");
	}
}
