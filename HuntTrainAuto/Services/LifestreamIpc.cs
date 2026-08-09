#nullable enable
using System;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using HuntTrainAuto.Contracts;
using HuntTrainAuto.Logging;

namespace HuntTrainAuto.Services;

/// <summary>
/// Soft-fail wrapper around the Lifestream plugin IPC.
/// HTA uses <c>EzIPC.Init(this, "Lifestream", …)</c> so channels are prefixed
/// <c>Lifestream.*</c> (see NightmareXIV/Lifestream <c>IPCProvider</c>).
/// </summary>
public sealed class LifestreamIpc : ILifestreamService
{
	/// <summary>IPC: <c>Lifestream.Teleport</c> — <c>Func&lt;uint, byte, bool&gt;</c>.</summary>
	private const string TeleportChannel = "Lifestream.Teleport";

	/// <summary>IPC: <c>Lifestream.ChangeInstance</c> — <c>Action&lt;int&gt;</c>.</summary>
	private const string ChangeInstanceChannel = "Lifestream.ChangeInstance";

	/// <summary>IPC: <c>Lifestream.GetCurrentInstance</c> — <c>Func&lt;int&gt;</c>.</summary>
	private const string GetCurrentInstanceChannel = "Lifestream.GetCurrentInstance";

	/// <summary>IPC: <c>Lifestream.GetNumberOfInstances</c> — <c>Func&lt;int&gt;</c>.</summary>
	private const string GetNumberOfInstancesChannel = "Lifestream.GetNumberOfInstances";

	/// <summary>IPC: <c>Lifestream.CanChangeInstance</c> — <c>Func&lt;bool&gt;</c>.</summary>
	private const string CanChangeInstanceChannel = "Lifestream.CanChangeInstance";

	/// <summary>IPC: <c>Lifestream.IsBusy</c> — <c>Func&lt;bool&gt;</c>.</summary>
	private const string IsBusyChannel = "Lifestream.IsBusy";

	/// <summary>IPC: <c>Lifestream.Abort</c> — <c>Action</c>.</summary>
	private const string AbortChannel = "Lifestream.Abort";

	/// <summary>IPC: <c>Lifestream.CanVisitSameDC</c> — <c>Func&lt;string, bool&gt;</c>.</summary>
	private const string CanVisitSameDcChannel = "Lifestream.CanVisitSameDC";

	/// <summary>IPC: <c>Lifestream.CanVisitCrossDC</c> — <c>Func&lt;string, bool&gt;</c>.</summary>
	private const string CanVisitCrossDcChannel = "Lifestream.CanVisitCrossDC";

	/// <summary>
	/// IPC: <c>Lifestream.ChangeWorld</c> — <c>Func&lt;string, bool&gt;</c>.
	/// Validates via CanVisit* then queues <c>TPAndChangeWorld</c> (preferred over raw ExecuteCommand).
	/// </summary>
	private const string ChangeWorldChannel = "Lifestream.ChangeWorld";

	/// <summary>
	/// Probe args that should never resolve to a real aetheryte.
	/// A successful InvokeFunc only proves the CallGate provider exists.
	/// </summary>
	private const uint ProbeAetheryteId = uint.MaxValue;
	private const byte ProbeSubIndex = byte.MaxValue;

	private readonly ICallGateSubscriber<uint, byte, bool> teleport;
	private readonly ICallGateSubscriber<int, object> changeInstance;
	private readonly ICallGateSubscriber<int> getCurrentInstance;
	private readonly ICallGateSubscriber<int> getNumberOfInstances;
	private readonly ICallGateSubscriber<bool> canChangeInstance;
	private readonly ICallGateSubscriber<bool> isBusy;
	private readonly ICallGateSubscriber<object?> abort;
	private readonly ICallGateSubscriber<string, bool> canVisitSameDc;
	private readonly ICallGateSubscriber<string, bool> canVisitCrossDc;
	private readonly ICallGateSubscriber<string, bool> changeWorld;
	private readonly IPluginLog? log;
	private readonly Func<bool>? debugEnabled;

	public LifestreamIpc(
		IDalamudPluginInterface pluginInterface,
		IPluginLog? log = null,
		Func<bool>? debugEnabled = null)
	{
		this.log = log;
		this.debugEnabled = debugEnabled;
		teleport = pluginInterface.GetIpcSubscriber<uint, byte, bool>(TeleportChannel);
		changeInstance = pluginInterface.GetIpcSubscriber<int, object>(ChangeInstanceChannel);
		getCurrentInstance = pluginInterface.GetIpcSubscriber<int>(GetCurrentInstanceChannel);
		getNumberOfInstances = pluginInterface.GetIpcSubscriber<int>(GetNumberOfInstancesChannel);
		canChangeInstance = pluginInterface.GetIpcSubscriber<bool>(CanChangeInstanceChannel);
		isBusy = pluginInterface.GetIpcSubscriber<bool>(IsBusyChannel);
		abort = pluginInterface.GetIpcSubscriber<object?>(AbortChannel);
		canVisitSameDc = pluginInterface.GetIpcSubscriber<string, bool>(CanVisitSameDcChannel);
		canVisitCrossDc = pluginInterface.GetIpcSubscriber<string, bool>(CanVisitCrossDcChannel);
		changeWorld = pluginInterface.GetIpcSubscriber<string, bool>(ChangeWorldChannel);
	}

	/// <summary>
	/// True when the <c>Lifestream.Teleport</c> CallGate has a registered provider.
	/// Plugin-loaded alone is not enough (version skew / load order) — probes
	/// InvokeFunc with an invalid aetheryte; throw → unavailable, bool return → ready.
	/// Soft-fails: never throws to callers.
	/// </summary>
	public bool IsAvailable
	{
		get
		{
			try
			{
				// Gate exists if InvokeFunc returns (even false for invalid id).
				_ = teleport.InvokeFunc(ProbeAetheryteId, ProbeSubIndex);
				return true;
			}
			catch (Exception ex)
			{
				DebugSoftFail("availability probe", ex);
				return false;
			}
		}
	}

	/// <summary>
	/// Fallback aetheryte teleport via Lifestream (<c>Lifestream.Teleport</c>).
	/// Soft-fails (returns false) when Lifestream is absent or IPC throws.
	/// </summary>
	public bool Teleport(uint aetheryteId, byte subIndex = 0)
	{
		try
		{
			var accepted = teleport.InvokeFunc(aetheryteId, subIndex);
			Debug(accepted
				? $"teleport accepted: aetheryte={aetheryteId}, subIndex={subIndex}"
				: $"teleport declined: aetheryte={aetheryteId}, subIndex={subIndex}");
			return accepted;
		}
		catch (Exception ex)
		{
			DebugSoftFail("Teleport", ex);
			return false;
		}
	}

	/// <summary>
	/// Request an instance change (<c>Lifestream.ChangeInstance</c>). Soft-fails silently.
	/// </summary>
	public void ChangeInstance(int instance)
	{
		try
		{
			changeInstance.InvokeAction(instance);
			Debug($"ChangeInstance requested: {instance}");
		}
		catch (Exception ex)
		{
			DebugSoftFail("ChangeInstance", ex);
		}
	}

	/// <summary>
	/// Current instance number (<c>Lifestream.GetCurrentInstance</c>).
	/// Returns 0 when unavailable (unknown / shared), matching decision API convention.
	/// </summary>
	public int GetCurrentInstance()
	{
		try
		{
			return getCurrentInstance.InvokeFunc();
		}
		catch (Exception ex)
		{
			DebugSoftFail("GetCurrentInstance", ex);
			return 0;
		}
	}

	/// <summary>
	/// How many instances exist in the current area (<c>Lifestream.GetNumberOfInstances</c>).
	/// Returns 0 when unavailable.
	/// </summary>
	public int GetNumberOfInstances()
	{
		try
		{
			return getNumberOfInstances.InvokeFunc();
		}
		catch (Exception ex)
		{
			DebugSoftFail("GetNumberOfInstances", ex);
			return 0;
		}
	}

	/// <summary>
	/// Whether instance travel UI/actions are usable (<c>Lifestream.CanChangeInstance</c>).
	/// Soft-fails (returns false) when Lifestream is absent or IPC throws.
	/// </summary>
	public bool CanChangeInstance()
	{
		try
		{
			return canChangeInstance.InvokeFunc();
		}
		catch (Exception ex)
		{
			DebugSoftFail("CanChangeInstance", ex);
			return false;
		}
	}

	/// <summary>
	/// Whether Lifestream is mid-task (<c>Lifestream.IsBusy</c>).
	/// Soft-fails (returns false = not busy) when Lifestream is absent or IPC throws,
	/// so mount/wait gates are not stuck forever without the plugin.
	/// </summary>
	public bool IsBusy()
	{
		try
		{
			return isBusy.InvokeFunc();
		}
		catch (Exception ex)
		{
			DebugSoftFail("IsBusy", ex);
			return false;
		}
	}

	/// <summary>
	/// Abort the current Lifestream task / follow path (<c>Lifestream.Abort</c>).
	/// Soft-fails silently when Lifestream is absent or IPC throws.
	/// </summary>
	public void Abort()
	{
		try
		{
			abort.InvokeAction();
			Debug("Abort requested");
		}
		catch (Exception ex)
		{
			DebugSoftFail("Abort", ex);
		}
	}

	/// <summary>
	/// Same-DC world visit eligibility (<c>Lifestream.CanVisitSameDC</c>).
	/// Soft-fails (false) when Lifestream is absent or IPC throws.
	/// </summary>
	public bool CanVisitSameDC(string world)
	{
		try
		{
			return canVisitSameDc.InvokeFunc(world);
		}
		catch (Exception ex)
		{
			DebugSoftFail("CanVisitSameDC", ex);
			return false;
		}
	}

	/// <summary>
	/// Cross-DC world visit eligibility (<c>Lifestream.CanVisitCrossDC</c>).
	/// Soft-fails (false) when Lifestream is absent or IPC throws.
	/// </summary>
	public bool CanVisitCrossDC(string world)
	{
		try
		{
			return canVisitCrossDc.InvokeFunc(world);
		}
		catch (Exception ex)
		{
			DebugSoftFail("CanVisitCrossDC", ex);
			return false;
		}
	}

	/// <summary>
	/// Queue a world visit (<c>Lifestream.ChangeWorld</c>). Soft-fails (false) when
	/// Lifestream is absent, busy, or the world is not in CanVisit* lists.
	/// </summary>
	public bool ChangeWorld(string world)
	{
		try
		{
			var accepted = changeWorld.InvokeFunc(world);
			Debug(accepted ? $"ChangeWorld accepted: {world}" : $"ChangeWorld declined: {world}");
			return accepted;
		}
		catch (Exception ex)
		{
			DebugSoftFail("ChangeWorld", ex);
			return false;
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
			DebugBehavior.Debug(log, IsDebugEnabled(), "Lifestream", message);
	}

	private void DebugSoftFail(string operation, Exception ex)
	{
		if (log != null)
			DebugBehavior.DebugThrottled(
				log, IsDebugEnabled(), $"lifestream.{operation}", 2_000, Environment.TickCount64, "Lifestream",
				$"{operation} unavailable/soft-fail: {ex.Message}");
	}
}
