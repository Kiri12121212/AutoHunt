#nullable enable

using System;

namespace HuntTrainAuto.Contracts;

/// <summary>HuntAlerts EzIPC subscribe surface (TASKS 10.2).</summary>
public interface IHuntAlertsService : IDisposable
{
	/// <summary>True when HuntAlerts is installed and loaded.</summary>
	bool IsPluginLoaded { get; }
}
