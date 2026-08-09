#nullable enable
using System;
using System.Linq;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using HuntTrainAuto.Combat;
using HuntTrainAuto.Contracts;
using HuntTrainAuto.Logging;

namespace HuntTrainAuto.Services;

/// <summary>
/// Soft-fail wrapper around Rotation Solver Reborn IPC
/// (<see href="https://github.com/FFXIV-CombatReborn/RotationSolverReborn">RSR</see>).
/// Channels use EzIPC prefix <c>RotationSolverReborn</c>. Dalamud InternalName is
/// <c>RotationSolver</c>. Live CallGate cannot be unit-tested without Dalamud.
/// </summary>
public sealed class RsrIpc : IRsrService
{
	/// <summary>IPC: <c>RotationSolverReborn.ChangeOperatingMode</c> — <c>Action&lt;StateCommandType&gt;</c>.</summary>
	private const string ChangeOperatingModeChannel = RsrCommands.IpcPrefix + ".ChangeOperatingMode";

	/// <summary>
	/// IPC: <c>RotationSolverReborn.AutodutyChangeOperatingMode</c> —
	/// <c>Action&lt;StateCommandType, TargetingType&gt;</c>.
	/// </summary>
	private const string AutodutyChangeOperatingModeChannel =
		RsrCommands.IpcPrefix + ".AutodutyChangeOperatingMode";

	/// <summary>
	/// IPC: <c>RotationSolverReborn.OtherCommand</c> —
	/// <c>Action&lt;OtherCommandType, string&gt;</c>.
	/// </summary>
	private const string OtherCommandChannel = RsrCommands.IpcPrefix + ".OtherCommand";

	private readonly IDalamudPluginInterface pluginInterface;
	private readonly ICallGateSubscriber<RsrStateCommandType, object> changeOperatingMode;
	private readonly ICallGateSubscriber<RsrStateCommandType, RsrTargetingType, object> autodutyChangeOperatingMode;
	private readonly ICallGateSubscriber<RsrOtherCommandType, string, object> otherCommand;
	private readonly IPluginLog? log;
	private readonly Func<bool>? debugEnabled;

	public RsrIpc(
		IDalamudPluginInterface pluginInterface,
		IPluginLog? log = null,
		Func<bool>? debugEnabled = null)
	{
		this.pluginInterface = pluginInterface;
		this.log = log;
		this.debugEnabled = debugEnabled;
		changeOperatingMode = pluginInterface.GetIpcSubscriber<RsrStateCommandType, object>(
			ChangeOperatingModeChannel);
		autodutyChangeOperatingMode =
			pluginInterface.GetIpcSubscriber<RsrStateCommandType, RsrTargetingType, object>(
				AutodutyChangeOperatingModeChannel);
		otherCommand = pluginInterface.GetIpcSubscriber<RsrOtherCommandType, string, object>(
			OtherCommandChannel);
	}

	/// <inheritdoc />
	public bool IsEnabled
	{
		get
		{
			try
			{
				return RsrCommands.IsPluginLoaded(
					pluginInterface.InstalledPlugins.Select(p => (p.InternalName, p.IsLoaded)));
			}
			catch (Exception ex)
			{
				DebugSoftFail("availability probe", ex);
				return false;
			}
		}
	}

	/// <inheritdoc />
	public bool IsAvailable => IsEnabled;

	/// <inheritdoc />
	public bool RotationAuto(RsrTargetingType targeting, RsrTargetHostileType hostileType)
	{
		foreach (var setting in RsrCommands.DefaultRotationAutoSettings(hostileType))
			OtherCommand(RsrOtherCommandType.Settings, setting);

		try
		{
			autodutyChangeOperatingMode.InvokeAction(RsrStateCommandType.AutoDuty, targeting);
			Debug($"RotationAuto succeeded: targeting={targeting}, hostile={hostileType}");
			return true;
		}
		catch (Exception ex)
		{
			DebugSoftFail("RotationAuto", ex);
			return false;
		}
	}

	/// <inheritdoc />
	public bool RotationStop()
	{
		try
		{
			changeOperatingMode.InvokeAction(RsrStateCommandType.Off);
			Debug("RotationStop succeeded");
			return true;
		}
		catch (Exception ex)
		{
			DebugSoftFail("RotationStop", ex);
			return false;
		}
	}

	private void OtherCommand(RsrOtherCommandType type, string command)
	{
		try
		{
			otherCommand.InvokeAction(type, command);
		}
		catch (Exception ex)
		{
			DebugSoftFail($"OtherCommand({type})", ex);
		}
	}

	public void Dispose()
	{
		// Subscriber only — no event subscriptions to tear down.
	}

	private bool IsDebugEnabled()
		=> debugEnabled?.Invoke() ?? false;

	private void Debug(string message)
	{
		if (log != null)
			DebugBehavior.Debug(log, IsDebugEnabled(), "RSR", message);
	}

	private void DebugSoftFail(string operation, Exception ex)
	{
		if (log != null)
			DebugBehavior.DebugThrottled(
				log, IsDebugEnabled(), $"rsr.{operation}", 2_000, Environment.TickCount64, "RSR",
				$"{operation} unavailable/soft-fail: {ex.Message}");
	}
}
