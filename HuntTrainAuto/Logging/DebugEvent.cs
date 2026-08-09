#nullable enable
using System;

namespace HuntTrainAuto.Logging;

/// <summary>Kinds recorded for the Debug tab (TASKS 9.2 + full-app coverage).</summary>
public enum DebugEventKind
{
	FlagReceived = 0,
	PhaseChange,
	Mount,
	Unmount,
	FakeHunt,
	Divert,
	Engage,
	Teleport,
	Instance,
	Navigate,
	Combat,
	HuntAlerts,
	PartyFinder,
	Chat,
	Map,
}

/// <summary>One in-memory automation event for the Debug tab ring buffer.</summary>
public readonly struct DebugEvent
{
	public DateTimeOffset Timestamp { get; init; }

	public DebugEventKind Kind { get; init; }

	public string Message { get; init; }
}
