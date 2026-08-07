#nullable enable
using System;
using Dalamud.Plugin.Services;
using HuntTrainAuto.Contracts;

namespace HuntTrainAuto.Combat;

/// <summary>
/// Thin Framework wiring for RSR enable (TASKS 6.2).
/// Starts <see cref="IRsrService.RotationAuto"/> while in combat phase until success,
/// and <see cref="IRsrService.RotationStop"/> on exit until success (anti-sticky). Soft-fails.
/// </summary>
public sealed class RsrEnableHelper
{
	private readonly IRsrService rsr;
	private readonly IPluginLog pluginLog;
	private bool rotationAutoStarted;

	public RsrEnableHelper(IRsrService rsr, IPluginLog pluginLog)
	{
		this.rsr = rsr;
		this.pluginLog = pluginLog;
	}

	/// <summary>True after a successful StartAuto until a successful Stop.</summary>
	public bool RotationAutoStarted => rotationAutoStarted;

	/// <summary>
	/// One Framework tick after combat transition: start/stop RSR with soft-fail retry.
	/// </summary>
	public void Tick(bool inCombatPhase)
	{
		try
		{
			TickCore(inCombatPhase);
		}
		catch (Exception ex)
		{
			// Do not mutate rotationAutoStarted — preserve start/stop retry next tick.
			pluginLog.Debug($"RsrEnableHelper soft-fail: {ex.Message}");
		}
	}

	/// <summary>
	/// Stop RSR if we started it (new flag / territory / master off / dispose).
	/// Keeps the latch when stop soft-fails so a later Clear/Tick can retry.
	/// </summary>
	public void Clear()
	{
		try
		{
			if (!rotationAutoStarted)
			{
				return;
			}

			if (rsr.RotationStop())
			{
				rotationAutoStarted = false;
				return;
			}

			pluginLog.Debug("RsrEnableHelper clear: RotationStop failed; keeping latch for retry");
		}
		catch (Exception ex)
		{
			pluginLog.Debug($"RsrEnableHelper clear soft-fail: {ex.Message}");
		}
	}

	private void TickCore(bool inCombatPhase)
	{
		var kind = RsrEnableDecision.Decide(inCombatPhase, rotationAutoStarted);

		if (kind == RsrEnableKind.StartAuto)
		{
			var ok = rsr.RotationAuto();
			rotationAutoStarted = RsrEnableDecision.NextRotationAutoStarted(kind, ok, rotationAutoStarted);
			if (ok)
				pluginLog.Debug("RSR: RotationAuto (combat phase enter)");
			else
				pluginLog.Debug("RSR: RotationAuto soft-fail; will retry while InCombatPhase");
		}
		else if (kind == RsrEnableKind.Stop)
		{
			var ok = rsr.RotationStop();
			rotationAutoStarted = RsrEnableDecision.NextRotationAutoStarted(kind, ok, rotationAutoStarted);
			if (ok)
				pluginLog.Debug("RSR: RotationStop (combat phase exit)");
			else
				pluginLog.Debug("RSR: RotationStop soft-fail; will retry while latch held");
		}
	}
}
