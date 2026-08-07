#nullable enable

using System;
using HuntTrainAuto.HuntAlerts;

namespace HuntTrainAuto.Contracts;

/// <summary>HuntAlerts EzIPC subscribe surface (TASKS 10.2 / 10.6).</summary>
public interface IHuntAlertsService : IDisposable
{
	/// <summary>True when HuntAlerts is installed and loaded.</summary>
	bool IsPluginLoaded { get; }

	/// <summary>
	/// Loaded and assembly version ≥ <see cref="HuntAlertsAvailability.MinimumVersion"/>
	/// (Integrations availability indicator). Soft-fails to false.
	/// </summary>
	bool IsAvailable { get; }

	/// <summary>
	/// Presence/version classification for the Integrations tab. Soft-fails to
	/// <see cref="HuntAlertsPluginStatus.Missing"/>.
	/// </summary>
	HuntAlertsPluginStatus PluginStatus { get; }

	/// <summary>Last successfully mapped HuntAlerts flag, if any.</summary>
	HuntAlertsLastAlert? LastMappedAlert { get; }
}
