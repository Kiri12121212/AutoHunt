#nullable enable
using System;
using Dalamud.Plugin.Services;
using HuntTrainAuto.Contracts;

namespace HuntTrainAuto.Combat;

/// <summary>
/// Thin Framework wiring for RSR enable (TASKS 6.2–6.4).
/// Starts <see cref="IRsrService.RotationAuto"/> while in combat phase until success,
/// and <see cref="IRsrService.RotationStop"/> on exit until success (anti-sticky). Soft-fails.
/// Targeting / HostileType come from <paramref name="resolveSettings"/> (config + role).
/// </summary>
public sealed class RsrEnableHelper
{
	private readonly IRsrService rsr;
	private readonly IPluginLog pluginLog;
	private readonly Func<(RsrTargetingType Targeting, RsrTargetHostileType Hostile)> resolveSettings;
	private bool rotationAutoStarted;

	public RsrEnableHelper(
		IRsrService rsr,
		IPluginLog pluginLog,
		Func<(RsrTargetingType Targeting, RsrTargetHostileType Hostile)> resolveSettings)
	{
		this.rsr = rsr;
		this.pluginLog = pluginLog;
		this.resolveSettings = resolveSettings;
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
			pluginLog.Debug($"[RSR] enable soft-fail: {ex.Message}");
		}
	}

	/// <summary>
	/// Stop RSR if we started it (flag / territory / master off / dispose — TASKS 6.5).
	/// Keeps the latch when stop soft-fails so a later Clear/Tick can retry
	/// (<see cref="RsrStopDecision.DecideClear"/> + shared NextRotationAutoStarted).
	/// </summary>
	public void Clear()
	{
		try
		{
			var kind = RsrStopDecision.DecideClear(rotationAutoStarted);
			if (kind == RsrEnableKind.None)
				return;

			var ok = rsr.RotationStop();
			rotationAutoStarted = RsrEnableDecision.NextRotationAutoStarted(kind, ok, rotationAutoStarted);
			if (ok)
				pluginLog.Debug($"[RSR] RotationStop (abort clear; {RsrEnableDecision.Describe(kind)})");
			else
				pluginLog.Debug("[RSR] RotationStop failed; keeping latch for retry");
		}
		catch (Exception ex)
		{
			pluginLog.Debug($"[RSR] clear soft-fail: {ex.Message}");
		}
	}

	/// <summary>
	/// Always <c>RotationStop</c> — clears sticky AutoDuty left on after reload / prior combat
	/// (HTA latch alone is not enough when AutoOffAfterCombat is false).
	/// </summary>
	public bool ForceStop(string reason)
	{
		try
		{
			var ok = rsr.RotationStop();
			if (ok)
			{
				rotationAutoStarted = false;
				pluginLog.Information($"[RSR] RotationStop ({reason})");
			}
			else
				pluginLog.Debug($"[RSR] ForceStop soft-fail ({reason})");
			return ok;
		}
		catch (Exception ex)
		{
			pluginLog.Debug($"[RSR] ForceStop soft-fail: {ex.Message}");
			return false;
		}
	}

	private void TickCore(bool inCombatPhase)
	{
		// Start via enable decision; Stop via shared stop/enable Decide (death / mob dead / phase exit).
		var kind = RsrStopDecision.DecideTick(inCombatPhase, rotationAutoStarted);

		if (kind == RsrEnableKind.StartAuto)
		{
			var (targeting, hostile) = resolveSettings();
			var ok = rsr.RotationAuto(targeting, hostile);
			rotationAutoStarted = RsrEnableDecision.NextRotationAutoStarted(kind, ok, rotationAutoStarted);
			if (ok)
				pluginLog.Debug(
					$"[RSR] RotationAuto {RsrEnableDecision.Describe(kind)} "
					+ RsrSettingsDecision.Describe(targeting, hostile));
			else
				pluginLog.Debug("[RSR] RotationAuto soft-fail; will retry while InCombatPhase");
		}
		else if (kind == RsrEnableKind.Stop)
		{
			var ok = rsr.RotationStop();
			rotationAutoStarted = RsrEnableDecision.NextRotationAutoStarted(kind, ok, rotationAutoStarted);
			if (ok)
				pluginLog.Debug($"[RSR] RotationStop (combat phase exit; {RsrEnableDecision.Describe(kind)})");
			else
				pluginLog.Debug("[RSR] RotationStop soft-fail; will retry while latch held");
		}
	}
}
