#nullable enable

namespace HuntTrainAuto.Logging;

/// <summary>
/// Pure formatters / gates for Debug tab events (TASKS 9.2).
/// </summary>
public static class DebugEventFormatter
{
	public static bool ShouldRecord(bool debugLoggingEnabled)
		=> debugLoggingEnabled;

	public static string KindLabel(DebugEventKind kind)
		=> kind switch
		{
			DebugEventKind.FlagReceived => "Flag",
			DebugEventKind.PhaseChange => "State",
			DebugEventKind.Mount => "Mount",
			DebugEventKind.Unmount => "Unmount",
			DebugEventKind.FakeHunt => "FakeHunt",
			DebugEventKind.Divert => "Divert",
			DebugEventKind.Engage => "Engage",
			DebugEventKind.Teleport => "Teleport",
			DebugEventKind.Instance => "Instance",
			DebugEventKind.Navigate => "Navigate",
			DebugEventKind.Combat => "Combat",
			DebugEventKind.HuntAlerts => "HuntAlerts",
			DebugEventKind.PartyFinder => "PF",
			DebugEventKind.Chat => "Chat",
			DebugEventKind.Map => "Map",
			_ => kind.ToString(),
		};

	public static string FormatLine(in DebugEvent e)
		=> $"[{e.Timestamp:HH:mm:ss}] {KindLabel(e.Kind)}: {e.Message}";

	public static string FormatPhaseChange(HuntTrainPhase from, HuntTrainPhase to)
		=> $"{StatusDisplay.FormatPhase(from)} → {StatusDisplay.FormatPhase(to)}";

	public static string FormatFlagReceived(string? placeName)
	{
		var trimmed = placeName?.Trim();
		return string.IsNullOrEmpty(trimmed)
			? "Conductor flag received"
			: $"Conductor flag: {trimmed}";
	}

	public static string FormatMountPhase(MountPhase phase)
		=> phase switch
		{
			MountPhase.Idle => "idle",
			MountPhase.WaitReady => "wait ready",
			MountPhase.Mounting => "mounting",
			_ => phase.ToString(),
		};

	public static string FormatUnmountPhase(UnmountPhase phase)
		=> phase switch
		{
			UnmountPhase.Idle => "idle",
			UnmountPhase.WaitReady => "wait ready",
			UnmountPhase.Unmounting => "unmounting",
			_ => phase.ToString(),
		};

	public static string FormatMountChange(MountPhase from, MountPhase to)
		=> $"{FormatMountPhase(from)} → {FormatMountPhase(to)}";

	public static string FormatUnmountChange(UnmountPhase from, UnmountPhase to)
		=> $"{FormatUnmountPhase(from)} → {FormatUnmountPhase(to)}";
}
