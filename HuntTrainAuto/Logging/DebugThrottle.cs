#nullable enable
using System;
using System.Collections.Generic;

namespace HuntTrainAuto.Logging;

/// <summary>
/// Rate-limit repetitive Framework-tick debug lines by key.
/// </summary>
public static class DebugThrottle
{
	private static readonly Dictionary<string, long> LastLogMs = new(StringComparer.Ordinal);

	/// <summary>
	/// True when <paramref name="key"/> has not logged within <paramref name="intervalMs"/>.
	/// Updates the stamp when returning true.
	/// </summary>
	public static bool Try(string key, int intervalMs, long nowMs)
	{
		if (string.IsNullOrEmpty(key))
			return true;

		var wait = intervalMs > 0 ? intervalMs : 0;
		if (LastLogMs.TryGetValue(key, out var last) && nowMs - last < wait)
			return false;

		LastLogMs[key] = nowMs;
		return true;
	}

	/// <summary>Clear stamps (tests / plugin dispose).</summary>
	public static void Reset() => LastLogMs.Clear();
}
