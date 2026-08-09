#nullable enable
using System;

namespace HuntTrainAuto.Logging;

/// <summary>
/// Edge-detects pipeline signals and appends to <see cref="DebugEventLog"/> (TASKS 9.2).
/// Call from Framework / flag handlers; soft no-op when logging disabled.
/// </summary>
public sealed class DebugEventProbe
{
	private readonly DebugEventLog log;
	private HuntTrainPhase lastPhase = HuntTrainPhase.Idle;
	private MountPhase lastMount = MountPhase.Idle;
	private UnmountPhase lastUnmount = UnmountPhase.Idle;
	private bool initialized;

	public DebugEventProbe(DebugEventLog log)
	{
		this.log = log;
	}

	public void RecordFlagReceived(bool debugEnabled, string? placeName)
	{
		if (!DebugEventFormatter.ShouldRecord(debugEnabled))
			return;

		log.Record(DebugEventKind.FlagReceived, DebugEventFormatter.FormatFlagReceived(placeName));
	}

	public void Record(bool debugEnabled, DebugEventKind kind, string message)
	{
		if (!DebugEventFormatter.ShouldRecord(debugEnabled))
			return;

		log.Record(kind, message);
	}

	/// <summary>
	/// Observe live pipeline edges. Seeds baselines on first call without recording noise.
	/// </summary>
	public void Observe(
		bool debugEnabled,
		HuntTrainPhase phase,
		MountPhase mountPhase,
		UnmountPhase unmountPhase)
	{
		if (!DebugEventFormatter.ShouldRecord(debugEnabled))
		{
			lastPhase = phase;
			lastMount = mountPhase;
			lastUnmount = unmountPhase;
			initialized = true;
			return;
		}

		if (!initialized)
		{
			lastPhase = phase;
			lastMount = mountPhase;
			lastUnmount = unmountPhase;
			initialized = true;
			return;
		}

		if (phase != lastPhase)
		{
			log.Record(DebugEventKind.PhaseChange, DebugEventFormatter.FormatPhaseChange(lastPhase, phase));
			lastPhase = phase;
		}

		if (mountPhase != lastMount)
		{
			log.Record(DebugEventKind.Mount, DebugEventFormatter.FormatMountChange(lastMount, mountPhase));
			lastMount = mountPhase;
		}

		if (unmountPhase != lastUnmount)
		{
			log.Record(DebugEventKind.Unmount, DebugEventFormatter.FormatUnmountChange(lastUnmount, unmountPhase));
			lastUnmount = unmountPhase;
		}
	}
}
