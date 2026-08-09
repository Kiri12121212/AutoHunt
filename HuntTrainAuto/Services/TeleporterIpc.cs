#nullable enable
using System;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using HuntTrainAuto.Contracts;
using HuntTrainAuto.Logging;

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
	private readonly IPluginLog? log;
	private readonly Func<bool>? debugEnabled;

	public TeleporterIpc(
		IDalamudPluginInterface pluginInterface,
		IPluginLog? log = null,
		Func<bool>? debugEnabled = null)
	{
		this.log = log;
		this.debugEnabled = debugEnabled;
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
			catch (Exception ex)
			{
				DebugSoftFail("availability probe", ex);
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

	public void Dispose()
	{
		// Subscriber only — no event subscriptions to tear down.
	}

	private bool IsDebugEnabled()
		=> debugEnabled?.Invoke() ?? false;

	private void Debug(string message)
	{
		if (log != null)
			DebugBehavior.Debug(log, IsDebugEnabled(), "Teleporter", message);
	}

	private void DebugSoftFail(string operation, Exception ex)
	{
		if (log != null)
			DebugBehavior.DebugThrottled(
				log, IsDebugEnabled(), $"teleporter.{operation}", 2_000, Environment.TickCount64, "Teleporter",
				$"{operation} unavailable/soft-fail: {ex.Message}");
	}
}
