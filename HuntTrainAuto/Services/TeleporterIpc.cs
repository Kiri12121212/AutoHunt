#nullable enable
using System;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using HuntTrainAuto.Contracts;

namespace HuntTrainAuto.Services;

/// <summary>
/// Soft-fail wrapper around the Teleporter plugin IPC.
/// Channel is the bare name <c>"Teleport"</c> — HTA's
/// <c>[EzIPC(applyPrefix: false)] Func&lt;uint, byte, bool&gt; Teleport</c>
/// with <c>EzIPC.Init(this, "Teleport", …)</c> resolves to that tag
/// (not <c>"Teleporter.Teleport"</c>).
/// </summary>
public sealed class TeleporterIpc : ITeleporterService
{
	/// <summary>
	/// IPC tag registered by Teleporter / consumed by HTA TeleporterIPC.
	/// Bare <c>"Teleport"</c> — see class summary.
	/// </summary>
	private const string TeleportChannel = "Teleport";

	/// <summary>
	/// Probe args that should never resolve to a real aetheryte.
	/// A successful InvokeFunc only proves the CallGate provider exists.
	/// </summary>
	private const uint ProbeAetheryteId = uint.MaxValue;
	private const byte ProbeSubIndex = byte.MaxValue;

	private readonly ICallGateSubscriber<uint, byte, bool> teleport;

	public TeleporterIpc(IDalamudPluginInterface pluginInterface)
	{
		teleport = pluginInterface.GetIpcSubscriber<uint, byte, bool>(TeleportChannel);
	}

	/// <summary>
	/// True when the <c>"Teleport"</c> CallGate has a registered provider.
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
	/// Request a teleport to <paramref name="aetheryteId"/>. Soft-fails (returns false)
	/// when Teleporter is absent or IPC throws.
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

	public void Dispose()
	{
		// Subscriber only — no event subscriptions to tear down.
	}
}
