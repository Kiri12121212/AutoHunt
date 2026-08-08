#nullable enable
using System;

namespace HuntTrainAuto.Logging;

/// <summary>Kinds recorded for the Debug tab (TASKS 9.2).</summary>
public enum DebugEventKind
{
	FlagReceived = 0,
	PhaseChange,
	Mount,
	Unmount,
}

/// <summary>One in-memory automation event for the Debug tab ring buffer.</summary>
public readonly struct DebugEvent
{
	public DateTimeOffset Timestamp { get; init; }

	public DebugEventKind Kind { get; init; }

	public string Message { get; init; }
}
