#nullable enable
using System;
using Dalamud.Plugin.Services;
using HuntTrainAuto.Contracts;

namespace HuntTrainAuto.Combat;

/// <summary>
/// Framework wiring for BossMod AI enable — mirrors <see cref="RsrEnableHelper"/>.
/// Starts AI while in combat phase until success; disables on exit / Clear until success.
/// Soft-fails. Gated by <paramref name="isIntegrationEnabled"/>.
/// </summary>
public sealed class BossModEnableHelper
{
	private readonly IBossModService bossMod;
	private readonly IPluginLog pluginLog;
	private readonly Func<bool> isIntegrationEnabled;
	private bool aiStarted;

	public BossModEnableHelper(
		IBossModService bossMod,
		IPluginLog pluginLog,
		Func<bool> isIntegrationEnabled)
	{
		this.bossMod = bossMod;
		this.pluginLog = pluginLog;
		this.isIntegrationEnabled = isIntegrationEnabled;
	}

	/// <summary>True after a successful EnableAi until a successful DisableAi.</summary>
	public bool AiStarted => aiStarted;

	/// <summary>
	/// One Framework tick: start/stop BM AI with soft-fail retry.
	/// When integration is off, forces stop if latch held.
	/// </summary>
	public void Tick(bool inCombatPhase)
	{
		try
		{
			TickCore(inCombatPhase);
		}
		catch (Exception ex)
		{
			pluginLog.Debug($"[BossMod] enable soft-fail: {ex.Message}");
		}
	}

	/// <summary>
	/// Stop BM AI if we started it (flag / territory / master off / dispose).
	/// Keeps the latch when disable soft-fails so a later Clear/Tick can retry.
	/// </summary>
	public void Clear()
	{
		try
		{
			var kind = BossModEnableDecision.DecideClear(aiStarted);
			if (kind == BossModEnableKind.None)
			{
				pluginLog.Debug($"[BossMod] clear skipped; {BossModEnableDecision.Describe(kind)}");
				return;
			}

			var ok = bossMod.DisableAi();
			aiStarted = BossModEnableDecision.NextAiStarted(kind, ok, aiStarted);
			if (ok)
				pluginLog.Debug($"[BossMod] AI off (abort clear; {BossModEnableDecision.Describe(kind)})");
			else
				pluginLog.Debug("[BossMod] DisableAi failed; keeping latch for retry");
		}
		catch (Exception ex)
		{
			pluginLog.Debug($"[BossMod] clear soft-fail: {ex.Message}");
		}
	}

	/// <summary>
	/// Always <c>DisableAi</c> — sticky AI left on after reload / prior combat.
	/// </summary>
	public bool ForceStop(string reason)
	{
		try
		{
			if (!bossMod.IsAvailable)
			{
				aiStarted = false;
				pluginLog.Debug("[BossMod] ForceStop skipped; provider unavailable");
				return true;
			}

			var ok = bossMod.DisableAi();
			var readback = bossMod.TryGetAiEnabled();
			if (readback is true)
				ok = false;
			if (ok)
			{
				aiStarted = false;
				pluginLog.Information($"[BossMod] AI off ({reason})");
			}
			else
				pluginLog.Debug($"[BossMod] ForceStop soft-fail ({reason})");
			return ok;
		}
		catch (Exception ex)
		{
			pluginLog.Debug($"[BossMod] ForceStop soft-fail: {ex.Message}");
			return false;
		}
	}

	private void TickCore(bool inCombatPhase)
	{
		var integrationOn = false;
		try
		{
			integrationOn = isIntegrationEnabled();
		}
		catch
		{
			integrationOn = false;
		}

		// Master integration off → treat as leave combat for stop/retry.
		var wantCombat = integrationOn && inCombatPhase;
		var kind = BossModEnableDecision.Decide(wantCombat, aiStarted);

		if (kind == BossModEnableKind.StartAi)
		{
			if (!bossMod.IsAvailable)
			{
				pluginLog.Debug(
					$"[BossMod] AI start skipped; provider unavailable; "
					+ BossModEnableDecision.Describe(kind));
				return;
			}

			var ok = bossMod.EnableAi(coexistWithRsr: true);
			var readback = bossMod.TryGetAiEnabled();
			if (readback is false)
				ok = false;
			aiStarted = BossModEnableDecision.NextAiStarted(kind, ok, aiStarted);
			if (ok)
			{
				pluginLog.Information(
					$"[BossMod] AI on {BossModEnableDecision.Describe(kind)} provider={bossMod.ActiveProvider}"
					+ (readback is bool rb ? $" enabled={rb}" : string.Empty));
			}
			else
				pluginLog.Debug("[BossMod] EnableAi soft-fail; will retry while InCombatPhase");
		}
		else if (kind == BossModEnableKind.Stop)
		{
			var ok = bossMod.DisableAi();
			aiStarted = BossModEnableDecision.NextAiStarted(kind, ok, aiStarted);
			if (ok)
				pluginLog.Information(
					$"[BossMod] AI off (combat phase exit / integration off; "
					+ BossModEnableDecision.Describe(kind) + ")");
			else
				pluginLog.Debug("[BossMod] DisableAi soft-fail; will retry while latch held");
		}
		else if (DebugThrottle.Try("bossmod.enable.skip", 2000, Environment.TickCount64))
		{
			var reason = integrationOn ? "already in desired state" : "integration disabled";
			pluginLog.Debug(
				$"[BossMod] skipped ({reason}); {BossModEnableDecision.Describe(kind)}");
		}
	}
}
