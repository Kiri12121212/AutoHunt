#nullable enable
using System;

namespace HuntTrainAuto.Windows;

/// <summary>
/// Pure helpers for dependency presence labels (TASKS 7.5).
/// IPC probes stay in service wrappers; this formats / soft-wraps bool probes only.
/// </summary>
public static class DependencyAvailability
{
	public const string TeleporterDisplayName = "Teleporter";
	public const string LifestreamDisplayName = "Lifestream";
	public const string VnavmeshDisplayName = "vnavmesh";
	public const string RsrDisplayName = "Rotation Solver Reborn";

	public const string AvailableLabel = "available";
	public const string MissingLabel = "missing";

	public static string StatusLabel(bool available)
		=> available ? AvailableLabel : MissingLabel;

	public static string FormatLine(string displayName, bool available)
		=> $"{displayName}: {StatusLabel(available)}";

	/// <summary>Invoke <paramref name="probe"/>; any throw → unavailable.</summary>
	public static bool SafeIsAvailable(Func<bool> probe)
	{
		try
		{
			return probe();
		}
		catch
		{
			return false;
		}
	}
}
