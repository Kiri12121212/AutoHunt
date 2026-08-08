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
			pluginLog.Debug($"BossModEnableHelper soft-fail: {ex.Message}");
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
				return;

			var ok = bossMod.DisableAi();
			aiStarted = BossModEnableDecision.NextAiStarted(kind, ok, aiStarted);
			if (ok)
				pluginLog.Debug("BossMod: AI off (abort clear)");
			else
				pluginLog.Debug("BossModEnableHelper clear: DisableAi failed; keeping latch for retry");
		}
		catch (Exception ex)
		{
			pluginLog.Debug($"BossModEnableHelper clear soft-fail: {ex.Message}");
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
				pluginLog.Debug("BossMod: AI start skipped; plugin not available");
				return;
			}

			var ok = bossMod.EnableAi(coexistWithRsr: true);
			aiStarted = BossModEnableDecision.NextAiStarted(kind, ok, aiStarted);
			if (ok)
				pluginLog.Debug($"BossMod: AI on provider={bossMod.ActiveProvider}");
			else
				pluginLog.Debug("BossMod: EnableAi soft-fail; will retry while InCombatPhase");
		}
		else if (kind == BossModEnableKind.Stop)
		{
			var ok = bossMod.DisableAi();
			aiStarted = BossModEnableDecision.NextAiStarted(kind, ok, aiStarted);
			if (ok)
				pluginLog.Debug("BossMod: AI off (combat phase exit / integration off)");
			else
				pluginLog.Debug("BossMod: DisableAi soft-fail; will retry while latch held");
		}
	}
}
