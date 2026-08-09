#nullable enable
using System;
using Dalamud.Plugin.Services;

namespace HuntTrainAuto.Logging;

/// <summary>
/// Gated Dalamud debug helpers: <see cref="Configuration.EnableDebugLogging"/> +
/// optional <see cref="DebugThrottle"/> for Framework-tick paths.
/// </summary>
public static class DebugBehavior
{
	/// <summary>
	/// Emit <paramref name="message"/> at Debug when enabled.
	/// Prefix with <c>[area]</c> when <paramref name="area"/> is non-empty.
	/// </summary>
	public static void Debug(IPluginLog log, bool enabled, string area, string message)
	{
		if (!enabled || log is null)
			return;
		log.Debug(Format(area, message));
	}

	/// <summary>
	/// Throttled Debug. Returns true when a line was emitted.
	/// </summary>
	public static bool DebugThrottled(
		IPluginLog log,
		bool enabled,
		string throttleKey,
		int intervalMs,
		long nowMs,
		string area,
		string message)
	{
		if (!enabled || log is null)
			return false;
		if (!DebugThrottle.Try(throttleKey, intervalMs, nowMs))
			return false;
		log.Debug(Format(area, message));
		return true;
	}

	/// <summary>Information-level edge (not gated by EnableDebugLogging).</summary>
	public static void Info(IPluginLog log, string area, string message)
	{
		if (log is null)
			return;
		log.Information(Format(area, message));
	}

	public static string Format(string area, string message)
	{
		var msg = message ?? string.Empty;
		var a = area?.Trim();
		return string.IsNullOrEmpty(a) ? msg : $"[{a}] {msg}";
	}
}
