#nullable enable
using System;
using System.Collections.Generic;

namespace HuntTrainAuto.Logging;

/// <summary>
/// Fixed-capacity ring buffer of debug events (TASKS 9.2). Pure / game-free.
/// Newest entries overwrite oldest when full.
/// </summary>
public sealed class DebugEventLog
{
	public const int DefaultCapacity = 100;

	private readonly DebugEvent[] buffer;
	private int next;
	private int count;

	public DebugEventLog(int capacity = DefaultCapacity)
	{
		if (capacity < 1)
			capacity = 1;
		buffer = new DebugEvent[capacity];
	}

	public int Capacity => buffer.Length;

	public int Count => count;

	/// <summary>Append an event; drops oldest when at capacity.</summary>
	public void Record(DebugEventKind kind, string message, DateTimeOffset? timestamp = null)
	{
		var msg = message ?? string.Empty;
		buffer[next] = new DebugEvent
		{
			Timestamp = timestamp ?? DateTimeOffset.UtcNow,
			Kind = kind,
			Message = msg,
		};
		next = (next + 1) % buffer.Length;
		if (count < buffer.Length)
			count++;
	}

	/// <summary>Newest-first snapshot for UI / tests.</summary>
	public IReadOnlyList<DebugEvent> SnapshotNewestFirst()
	{
		if (count == 0)
			return Array.Empty<DebugEvent>();

		var result = new DebugEvent[count];
		for (var i = 0; i < count; i++)
		{
			var idx = next - 1 - i;
			if (idx < 0)
				idx += buffer.Length;
			result[i] = buffer[idx];
		}

		return result;
	}

	public void Clear()
	{
		Array.Clear(buffer, 0, buffer.Length);
		next = 0;
		count = 0;
	}
}
