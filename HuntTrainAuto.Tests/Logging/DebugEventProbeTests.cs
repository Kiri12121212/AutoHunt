#nullable enable

namespace HuntTrainAuto.Tests.Logging;

public sealed class DebugEventProbeTests
{
	[Fact]
	public void Observe_disabled_is_noop()
	{
		var log = new DebugEventLog();
		var probe = new DebugEventProbe(log);
		probe.Observe(false, HuntTrainPhase.Teleport, MountPhase.Idle, UnmountPhase.Idle);
		probe.Observe(false, HuntTrainPhase.Mount, MountPhase.Mounting, UnmountPhase.Idle);
		Assert.Equal(0, log.Count);
	}

	[Fact]
	public void Observe_records_edges_after_seed()
	{
		var log = new DebugEventLog();
		var probe = new DebugEventProbe(log);
		probe.Observe(true, HuntTrainPhase.Idle, MountPhase.Idle, UnmountPhase.Idle);
		Assert.Equal(0, log.Count);

		probe.Observe(true, HuntTrainPhase.Teleport, MountPhase.WaitReady, UnmountPhase.Idle);
		Assert.Equal(2, log.Count);
		var snap = log.SnapshotNewestFirst();
		Assert.Contains(snap, e => e.Kind == DebugEventKind.PhaseChange);
		Assert.Contains(snap, e => e.Kind == DebugEventKind.Mount);
	}

	[Fact]
	public void RecordFlagReceived_respects_toggle()
	{
		var log = new DebugEventLog();
		var probe = new DebugEventProbe(log);
		probe.RecordFlagReceived(false, "X");
		Assert.Equal(0, log.Count);
		probe.RecordFlagReceived(true, "X");
		Assert.Equal(1, log.Count);
		Assert.Equal(DebugEventKind.FlagReceived, log.SnapshotNewestFirst()[0].Kind);
	}

	[Fact]
	public void Observe_records_flag_restart_idle_intermediate()
	{
		// Mid-pipeline AbortThenRestart: probe after Reset (Idle) then after Start* Apply.
		var log = new DebugEventLog();
		var probe = new DebugEventProbe(log);
		probe.Observe(true, HuntTrainPhase.Combat, MountPhase.Idle, UnmountPhase.Idle);

		probe.Observe(true, HuntTrainPhase.Idle, MountPhase.Idle, UnmountPhase.Idle);
		probe.Observe(true, HuntTrainPhase.Teleport, MountPhase.Idle, UnmountPhase.Idle);

		Assert.Equal(2, log.Count);
		var snap = log.SnapshotNewestFirst();
		Assert.Equal(
			DebugEventFormatter.FormatPhaseChange(HuntTrainPhase.Idle, HuntTrainPhase.Teleport),
			snap[0].Message);
		Assert.Equal(
			DebugEventFormatter.FormatPhaseChange(HuntTrainPhase.Combat, HuntTrainPhase.Idle),
			snap[1].Message);
	}
}
