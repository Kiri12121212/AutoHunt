#nullable enable

using System;
using System.Collections.Generic;
using HuntTrainAuto.Domain;

namespace HuntTrainAuto.HuntAlerts;

/// <summary>
/// Pure gates for HuntAlerts IPC subscribe/handle and settings availability (TASKS 10.2 / 10.6).
/// No CallGate — unit-tested without Dalamud.
/// </summary>
public static class HuntAlertsAvailability
{
	/// <summary>Dalamud InternalName for the HuntAlerts plugin.</summary>
	public const string PluginInternalName = "HuntAlerts";

	/// <summary>UI display name (Integrations tab / DependencyAvailability style).</summary>
	public const string DisplayName = "HuntAlerts";

	/// <summary>
	/// Minimum HuntAlerts assembly version required for the availability indicator
	/// (HTA <c>TabIntegrations</c> / ECommons <c>PluginAvailabilityIndicator</c>).
	/// </summary>
	public static readonly Version MinimumVersion = new(1, 2, 1, 3);

	public const string AvailableLabel = "available";
	public const string MissingLabel = "missing";
	public const string OutdatedLabel = "outdated";

	public const string LastAlertNoneLabel = "none";

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
	/// True when version is non-null and ≥ <see cref="MinimumVersion"/>.
	/// </summary>
	public static bool MeetsMinimumVersion(Version? version)
		=> version != null && version >= MinimumVersion;

	/// <summary>
	/// Classify HuntAlerts from an <c>InstalledPlugins</c> snapshot (loaded + version).
	/// Soft-callers wrap probes that may throw.
	/// </summary>
	public static HuntAlertsPluginStatus Evaluate(
		IEnumerable<(string InternalName, bool IsLoaded, Version? Version)> plugins)
	{
		var loaded = false;
		Version? found = null;
		foreach (var plugin in plugins)
		{
			if (!plugin.IsLoaded)
				continue;
			if (!string.Equals(
				    plugin.InternalName,
				    PluginInternalName,
				    StringComparison.Ordinal))
				continue;

			loaded = true;
			found = plugin.Version;
			break;
		}

		if (!loaded)
			return HuntAlertsPluginStatus.Missing;
		if (!MeetsMinimumVersion(found))
			return HuntAlertsPluginStatus.Outdated;
		return HuntAlertsPluginStatus.Available;
	}

	/// <summary>Loaded and version ≥ <see cref="MinimumVersion"/>.</summary>
	public static bool IsAvailable(
		IEnumerable<(string InternalName, bool IsLoaded, Version? Version)> plugins)
		=> Evaluate(plugins) == HuntAlertsPluginStatus.Available;

	/// <summary>Invoke <paramref name="probe"/>; any throw → <see cref="HuntAlertsPluginStatus.Missing"/>.</summary>
	public static HuntAlertsPluginStatus SafeEvaluate(Func<HuntAlertsPluginStatus> probe)
	{
		try
		{
			return probe();
		}
		catch
		{
			return HuntAlertsPluginStatus.Missing;
		}
	}

	public static string StatusLabel(HuntAlertsPluginStatus status)
		=> status switch
		{
			HuntAlertsPluginStatus.Available => AvailableLabel,
			HuntAlertsPluginStatus.Outdated => OutdatedLabel,
			_ => MissingLabel,
		};

	/// <summary>
	/// Integrations-tab line: <c>HuntAlerts: available</c> / <c>missing</c> / <c>outdated</c>.
	/// Outdated appends the minimum version hint (<c>1.2.1.3+</c>).
	/// </summary>
	public static string FormatAvailabilityLine(HuntAlertsPluginStatus status)
		=> status == HuntAlertsPluginStatus.Outdated
			? $"{DisplayName}: {OutdatedLabel} ({MinimumVersion}+)"
			: $"{DisplayName}: {StatusLabel(status)}";

	/// <summary>
	/// Compact last-mapped flag summary for the Integrations tab.
	/// Null / empty → <c>Last alert: none</c>.
	/// </summary>
	public static string FormatLastAlertStatus(HuntAlertsLastAlert? last)
	{
		if (last == null)
			return $"Last alert: {LastAlertNoneLabel}";

		var where = FormatLastAlertWhere(last.World, last.PlaceName, last.TerritoryTypeId);
		var when = last.Timestamp.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
		return string.IsNullOrEmpty(where)
			? $"Last alert: {when} UTC"
			: $"Last alert: {where} @ {when} UTC";
	}

	/// <summary>
	/// Compact last IPC intake line for Integrations diagnostics.
	/// Null / blank → <c>Last intake: none</c>.
	/// </summary>
	public static string FormatLastIntakeStatus(string? status)
		=> string.IsNullOrWhiteSpace(status)
			? $"Last intake: {LastAlertNoneLabel}"
			: status.StartsWith("Last intake:", StringComparison.Ordinal)
				? status
				: $"Last intake: {status}";

	/// <summary>Build a timestamped intake status line (pure).</summary>
	public static string FormatIntakeStatus(string detail, DateTimeOffset when)
	{
		var trimmed = string.IsNullOrWhiteSpace(detail) ? LastAlertNoneLabel : detail.Trim();
		var clock = when.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
		return $"Last intake: {trimmed} @ {clock} UTC";
	}

	/// <summary>Build a snapshot after a successful map (pure).</summary>
	public static HuntAlertsLastAlert FromMappedFlag(HuntFlag flag)
		=> new(
			flag.Timestamp,
			flag.HuntWorld,
			flag.PlaceName,
			flag.TerritoryTypeId);

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

	private static string FormatLastAlertWhere(
		string? world,
		string? placeName,
		uint territoryTypeId)
	{
		var w = string.IsNullOrWhiteSpace(world) ? null : world.Trim();
		var p = string.IsNullOrWhiteSpace(placeName) ? null : placeName.Trim();
		if (w != null && p != null)
			return $"{w} / {p}";
		if (w != null)
			return w;
		if (p != null)
			return p;
		if (territoryTypeId != 0)
			return $"Territory {territoryTypeId}";
		return string.Empty;
	}
}

/// <summary>HuntAlerts plugin presence for the Integrations availability indicator.</summary>
public enum HuntAlertsPluginStatus
{
	Missing = 0,
	Outdated = 1,
	Available = 2,
}

/// <summary>Last successfully mapped HuntAlerts flag (UI status; pure).</summary>
public sealed class HuntAlertsLastAlert
{
	public HuntAlertsLastAlert(
		DateTimeOffset timestamp,
		string? world,
		string? placeName,
		uint territoryTypeId)
	{
		Timestamp = timestamp;
		World = string.IsNullOrWhiteSpace(world) ? null : world.Trim();
		PlaceName = string.IsNullOrWhiteSpace(placeName) ? null : placeName.Trim();
		TerritoryTypeId = territoryTypeId;
	}

	public DateTimeOffset Timestamp { get; }

	public string? World { get; }

	public string? PlaceName { get; }

	public uint TerritoryTypeId { get; }
}
