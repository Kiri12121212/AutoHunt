#nullable enable

using System;
using System.Collections.Generic;

namespace HuntTrainAuto.HuntAlerts;

/// <summary>
/// Pure gates for HuntAlerts IPC subscribe/handle (TASKS 10.2).
/// No CallGate — unit-tested without Dalamud.
/// </summary>
public static class HuntAlertsAvailability
{
	/// <summary>Dalamud InternalName for the HuntAlerts plugin.</summary>
	public const string PluginInternalName = "HuntAlerts";

	/// <summary>
	/// EzIPC event tag published by HuntAlerts <c>IPCManager</c>
	/// (<c>Action&lt;HuntTrainMessage&gt;</c>).
	/// </summary>
	public const string OnHuntTrainMessageReceivedChannel =
		"HuntAlerts.OnHuntTrainMessageReceived";

	/// <summary>
	/// Typed EzIPC event tag (<c>Action&lt;HuntAlertMessage&gt;</c>) — reserved for later.
	/// </summary>
	public const string OnHuntAlertMessageReceivedChannel =
		"HuntAlerts.OnHuntAlertMessageReceived";

	/// <summary>
	/// True when HuntAlerts appears loaded in an <c>InstalledPlugins</c> snapshot.
	/// </summary>
	public static bool IsPluginLoaded(
		IEnumerable<(string InternalName, bool IsLoaded)> plugins)
	{
		foreach (var plugin in plugins)
		{
			if (plugin.IsLoaded
			    && string.Equals(
				    plugin.InternalName,
				    PluginInternalName,
				    StringComparison.Ordinal))
				return true;
		}

		return false;
	}

	/// <summary>
	/// Master gate for handling an IPC event: integration enabled and plugin loaded.
	/// </summary>
	public static bool ShouldHandle(bool huntAlertsIntegration, bool pluginLoaded)
		=> huntAlertsIntegration && pluginLoaded;

	/// <summary>
	/// Whether a received payload should be forwarded to the thin intake hook.
	/// Returns false when gated off or <paramref name="message"/> is null.
	/// </summary>
	public static bool TryAcceptMessage(
		bool huntAlertsIntegration,
		bool pluginLoaded,
		HuntTrainMessage? message,
		out HuntTrainMessage accepted)
	{
		accepted = null!;
		if (!ShouldHandle(huntAlertsIntegration, pluginLoaded))
			return false;
		if (message == null)
			return false;

		accepted = message;
		return true;
	}
}
