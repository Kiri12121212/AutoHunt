#nullable enable

namespace HuntTrainAuto.State;

/// <summary>
/// Top-level train pipeline phases (TASKS 7.1).
/// Distinct from local <see cref="CombatPhase"/> (follow→RSR only).
/// </summary>
public enum HuntTrainPhase
{
	Idle = 0,
	Teleport,
	Mount,
	Navigate,
	Unmount,
	FollowParty,
	Combat,
}

/// <summary>
/// Discrete signals for <see cref="HuntTrainTransition.Apply"/> / <see cref="HuntTrainController.Apply"/>.
/// Illegal (phase, event) pairs are no-ops.
/// </summary>
public enum HuntTrainEvent
{
	/// <summary>No transition.</summary>
	None = 0,

	/// <summary>Idle → Teleport when a flag needs TP.</summary>
	StartTeleport,

	/// <summary>Idle → Mount when same-zone / ready to move.</summary>
	StartMount,

	/// <summary>Idle → Navigate when already mounted / mount skipped.</summary>
	StartNavigate,

	/// <summary>Teleport → Mount when TP plan clears / arrived in zone.</summary>
	TeleportArrived,

	/// <summary>Mount → Navigate when mounted or mount skipped.</summary>
	MountReady,

	/// <summary>Navigate → Unmount when within flag arrival tolerance.</summary>
	FlagArrived,

	/// <summary>Unmount → FollowParty when dismounted / ReadyForGroundFollow.</summary>
	ReadyForGroundFollow,

	/// <summary>FollowParty → Combat when party engage / combat phase entered.</summary>
	EnterCombat,

	/// <summary>Combat → Idle when hunt dead / combat ended / clear.</summary>
	CombatEnded,

	/// <summary>Any active phase → Idle (master off, hard clear, abort).</summary>
	Abort,
}

/// <summary>
/// Optional Framework-tick snapshot for <see cref="HuntTrainTransition.Decide"/>.
/// Missing / false signals yield <see cref="HuntTrainEvent.None"/> (stay). Soft-fail: never throws.
/// </summary>
public readonly struct HuntTrainTickSnapshot
{
	/// <summary>Master <c>Config.Enabled</c>. False → Abort when not Idle.</summary>
	public bool PluginEnabled { get; init; }

	/// <summary>Hard abort / reset request.</summary>
	public bool Abort { get; init; }

	/// <summary>Flag needs teleport (Idle only).</summary>
	public bool NeedsTeleport { get; init; }

	/// <summary>Same-zone / ready to mount (Idle only; after NeedsTeleport).</summary>
	public bool SameZoneReady { get; init; }

	/// <summary>Already mounted or mount not required (Idle only; after NeedsTeleport).</summary>
	public bool AlreadyMountedOrSkipMount { get; init; }

	/// <summary>TP plan cleared / arrived in destination zone.</summary>
	public bool TeleportComplete { get; init; }

	/// <summary>Mounted or mount skipped.</summary>
	public bool MountComplete { get; init; }

	/// <summary>Within flag arrival tolerance / arrival signaled.</summary>
	public bool WithinFlagArrival { get; init; }

	/// <summary>Dismounted / ReadyForGroundFollow.</summary>
	public bool ReadyForGroundFollow { get; init; }

	/// <summary>Party engage / combat phase entered.</summary>
	public bool PartyEngaged { get; init; }

	/// <summary>Hunt dead / combat ended.</summary>
	public bool CombatEnded { get; init; }
}

/// <summary>
/// Pure legal transitions for the train orchestrator (TASKS 7.1).
/// No Dalamud hooks — unit-testable. Soft-fail: illegal events keep the phase.
/// </summary>
public static class HuntTrainTransition
{
	/// <summary>
	/// True when <paramref name="ev"/> moves <paramref name="from"/> to a different phase.
	/// <see cref="HuntTrainEvent.None"/> and illegal pairs return false and leave <paramref name="to"/> = <paramref name="from"/>.
	/// Abort while already Idle is a no-op (false).
	/// </summary>
	public static bool TryGetNext(HuntTrainPhase from, HuntTrainEvent ev, out HuntTrainPhase to)
	{
		to = from;

		if (ev == HuntTrainEvent.None)
			return false;

		if (ev == HuntTrainEvent.Abort)
		{
			if (from == HuntTrainPhase.Idle)
				return false;
			to = HuntTrainPhase.Idle;
			return true;
		}

		var next = (from, ev) switch
		{
			(HuntTrainPhase.Idle, HuntTrainEvent.StartTeleport) => HuntTrainPhase.Teleport,
			(HuntTrainPhase.Idle, HuntTrainEvent.StartMount) => HuntTrainPhase.Mount,
			(HuntTrainPhase.Idle, HuntTrainEvent.StartNavigate) => HuntTrainPhase.Navigate,
			(HuntTrainPhase.Teleport, HuntTrainEvent.TeleportArrived) => HuntTrainPhase.Mount,
			(HuntTrainPhase.Mount, HuntTrainEvent.MountReady) => HuntTrainPhase.Navigate,
			(HuntTrainPhase.Navigate, HuntTrainEvent.FlagArrived) => HuntTrainPhase.Unmount,
			(HuntTrainPhase.Unmount, HuntTrainEvent.ReadyForGroundFollow) => HuntTrainPhase.FollowParty,
			(HuntTrainPhase.FollowParty, HuntTrainEvent.EnterCombat) => HuntTrainPhase.Combat,
			(HuntTrainPhase.Combat, HuntTrainEvent.CombatEnded) => HuntTrainPhase.Idle,
			_ => from,
		};

		if (next == from)
			return false;

		to = next;
		return true;
	}

	/// <summary>Apply a single event; illegal / None → unchanged phase.</summary>
	public static HuntTrainPhase Apply(HuntTrainPhase phase, HuntTrainEvent ev)
		=> TryGetNext(phase, ev, out var next) ? next : phase;

	/// <summary>
	/// Choose at most one event for the current phase from a soft snapshot.
	/// Priority when Idle: NeedsTeleport → AlreadyMountedOrSkipMount → SameZoneReady.
	/// Master off / Abort → <see cref="HuntTrainEvent.Abort"/> when not Idle.
	/// </summary>
	public static HuntTrainEvent Decide(HuntTrainPhase phase, in HuntTrainTickSnapshot snap)
	{
		if (!snap.PluginEnabled || snap.Abort)
			return phase == HuntTrainPhase.Idle ? HuntTrainEvent.None : HuntTrainEvent.Abort;

		return phase switch
		{
			HuntTrainPhase.Idle => DecideIdleStart(snap),
			HuntTrainPhase.Teleport => snap.TeleportComplete
				? HuntTrainEvent.TeleportArrived
				: HuntTrainEvent.None,
			HuntTrainPhase.Mount => snap.MountComplete
				? HuntTrainEvent.MountReady
				: HuntTrainEvent.None,
			HuntTrainPhase.Navigate => snap.WithinFlagArrival
				? HuntTrainEvent.FlagArrived
				: HuntTrainEvent.None,
			HuntTrainPhase.Unmount => snap.ReadyForGroundFollow
				? HuntTrainEvent.ReadyForGroundFollow
				: HuntTrainEvent.None,
			HuntTrainPhase.FollowParty => snap.PartyEngaged
				? HuntTrainEvent.EnterCombat
				: HuntTrainEvent.None,
			HuntTrainPhase.Combat => snap.CombatEnded
				? HuntTrainEvent.CombatEnded
				: HuntTrainEvent.None,
			_ => HuntTrainEvent.None,
		};
	}

	/// <summary><see cref="Decide"/> then <see cref="Apply"/>.</summary>
	public static HuntTrainPhase Tick(HuntTrainPhase phase, in HuntTrainTickSnapshot snap)
		=> Apply(phase, Decide(phase, snap));

	private static HuntTrainEvent DecideIdleStart(in HuntTrainTickSnapshot snap)
	{
		if (snap.NeedsTeleport)
			return HuntTrainEvent.StartTeleport;
		if (snap.AlreadyMountedOrSkipMount)
			return HuntTrainEvent.StartNavigate;
		if (snap.SameZoneReady)
			return HuntTrainEvent.StartMount;
		return HuntTrainEvent.None;
	}
}
