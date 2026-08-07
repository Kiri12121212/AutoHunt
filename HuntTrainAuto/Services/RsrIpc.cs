#nullable enable
using System;
using System.Linq;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using HuntTrainAuto.Combat;
using HuntTrainAuto.Contracts;

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

	/// <summary>IPC: <c>RotationSolverReborn.AutorotationActive</c> — <c>Func&lt;bool&gt;</c>.</summary>
	private const string AutorotationActiveChannel = RsrCommands.IpcPrefix + ".AutorotationActive";

	private readonly IDalamudPluginInterface pluginInterface;
	private readonly ICallGateSubscriber<RsrStateCommandType, object> changeOperatingMode;
	private readonly ICallGateSubscriber<RsrStateCommandType, RsrTargetingType, object> autodutyChangeOperatingMode;
	private readonly ICallGateSubscriber<RsrOtherCommandType, string, object> otherCommand;
	private readonly ICallGateSubscriber<bool> autorotationActive;

	public RsrIpc(IDalamudPluginInterface pluginInterface)
	{
		this.pluginInterface = pluginInterface;
		changeOperatingMode = pluginInterface.GetIpcSubscriber<RsrStateCommandType, object>(
			ChangeOperatingModeChannel);
		autodutyChangeOperatingMode =
			pluginInterface.GetIpcSubscriber<RsrStateCommandType, RsrTargetingType, object>(
				AutodutyChangeOperatingModeChannel);
		otherCommand = pluginInterface.GetIpcSubscriber<RsrOtherCommandType, string, object>(
			OtherCommandChannel);
		autorotationActive = pluginInterface.GetIpcSubscriber<bool>(AutorotationActiveChannel);
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
			catch
			{
				return false;
			}
		}
	}

	/// <inheritdoc />
	public bool IsAvailable
	{
		get
		{
			try
			{
				_ = autorotationActive.InvokeFunc();
				return true;
			}
			catch
			{
				return false;
			}
		}
	}

	/// <inheritdoc />
	public bool RotationAuto()
		=> RotationAuto(RsrTargetingType.LowHP, RsrTargetHostileType.AllTargetsCanAttack);

	/// <summary>
	/// Same as <see cref="RotationAuto()"/> with explicit targeting / hostile defaults
	/// for later config wiring (6.3+). Soft-fails silently.
	/// </summary>
	/// <returns>True when <c>AutodutyChangeOperatingMode</c> succeeded.</returns>
	public bool RotationAuto(RsrTargetingType targeting, RsrTargetHostileType hostileType)
	{
		foreach (var setting in RsrCommands.DefaultRotationAutoSettings(hostileType))
			OtherCommand(RsrOtherCommandType.Settings, setting);

		try
		{
			autodutyChangeOperatingMode.InvokeAction(RsrStateCommandType.AutoDuty, targeting);
			return true;
		}
		catch
		{
			// RSR may be absent.
			return false;
		}
	}

	/// <inheritdoc />
	public bool RotationStop()
	{
		try
		{
			changeOperatingMode.InvokeAction(RsrStateCommandType.Off);
			return true;
		}
		catch
		{
			// RSR may be absent.
			return false;
		}
	}

	private void OtherCommand(RsrOtherCommandType type, string command)
	{
		try
		{
			otherCommand.InvokeAction(type, command);
		}
		catch
		{
			// RSR may be absent.
		}
	}

	public void Dispose()
	{
		// Subscriber only — no event subscriptions to tear down.
	}
}
