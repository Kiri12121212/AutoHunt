#nullable enable
using System;

namespace HuntTrainAuto.Tests.Logging;

public sealed class DebugEventLogTests
{
	[Fact]
	public void Record_appends_and_snapshots_newest_first()
	{
		var log = new DebugEventLog(capacity: 10);
		var t0 = new DateTimeOffset(2026, 8, 7, 12, 0, 0, TimeSpan.Zero);
		var t1 = t0.AddSeconds(1);
		log.Record(DebugEventKind.FlagReceived, "a", t0);
		log.Record(DebugEventKind.PhaseChange, "b", t1);

		Assert.Equal(2, log.Count);
		var snap = log.SnapshotNewestFirst();
		Assert.Equal(2, snap.Count);
		Assert.Equal("b", snap[0].Message);
		Assert.Equal("a", snap[1].Message);
		Assert.Equal(DebugEventKind.PhaseChange, snap[0].Kind);
	}

	[Fact]
	public void Record_overwrites_oldest_when_full()
	{
		var log = new DebugEventLog(capacity: 3);
		log.Record(DebugEventKind.Mount, "1");
		log.Record(DebugEventKind.Mount, "2");
		log.Record(DebugEventKind.Mount, "3");
		log.Record(DebugEventKind.Mount, "4");

		Assert.Equal(3, log.Count);
		Assert.Equal(3, log.Capacity);
		var snap = log.SnapshotNewestFirst();
		Assert.Equal(["4", "3", "2"], [snap[0].Message, snap[1].Message, snap[2].Message]);
	}

	[Fact]
	public void Clear_empties_buffer()
	{
		var log = new DebugEventLog(capacity: 4);
		log.Record(DebugEventKind.Unmount, "x");
		log.Clear();
		Assert.Equal(0, log.Count);
		Assert.Empty(log.SnapshotNewestFirst());
	}

	[Fact]
	public void Capacity_floors_at_one()
	{
		var log = new DebugEventLog(capacity: 0);
		Assert.Equal(1, log.Capacity);
		log.Record(DebugEventKind.Mount, "only");
		Assert.Equal(1, log.Count);
	}
}
