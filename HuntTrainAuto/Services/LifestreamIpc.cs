#nullable enable
using System;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;

namespace HuntTrainAuto.Services;

/// <summary>
/// Soft-fail wrapper around the Lifestream plugin IPC.
/// HTA uses <c>EzIPC.Init(this, "Lifestream", …)</c> so channels are prefixed
/// <c>Lifestream.*</c> (see NightmareXIV/Lifestream <c>IPCProvider</c>).
/// </summary>
public sealed class LifestreamIpc : IDisposable
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

	public LifestreamIpc(IDalamudPluginInterface pluginInterface)
	{
		teleport = pluginInterface.GetIpcSubscriber<uint, byte, bool>(TeleportChannel);
		changeInstance = pluginInterface.GetIpcSubscriber<int, object>(ChangeInstanceChannel);
		getCurrentInstance = pluginInterface.GetIpcSubscriber<int>(GetCurrentInstanceChannel);
		getNumberOfInstances = pluginInterface.GetIpcSubscriber<int>(GetNumberOfInstancesChannel);
		canChangeInstance = pluginInterface.GetIpcSubscriber<bool>(CanChangeInstanceChannel);
		isBusy = pluginInterface.GetIpcSubscriber<bool>(IsBusyChannel);
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
			catch
			{
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
			return teleport.InvokeFunc(aetheryteId, subIndex);
		}
		catch
		{
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
		}
		catch
		{
			// Lifestream may be absent or busy.
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
		catch
		{
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
		catch
		{
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
		catch
		{
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
		catch
		{
			return false;
		}
	}

	public void Dispose()
	{
		// Subscriber only — no event subscriptions to tear down.
	}
}
