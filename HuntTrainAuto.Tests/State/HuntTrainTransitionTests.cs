#nullable enable

namespace HuntTrainAuto.Tests.State;

public sealed class HuntTrainTransitionTests
{
	[Fact]
	public void Describe_formats_phase_edge()
	{
		Assert.Equal(
			"phase=Mount --StartTeleport--> Teleport",
			HuntTrainTransition.Describe(
				HuntTrainPhase.Mount,
				HuntTrainEvent.StartTeleport,
				HuntTrainPhase.Teleport));
	}

	[Theory]
	[InlineData(HuntTrainPhase.Idle, HuntTrainEvent.StartTeleport, HuntTrainPhase.Teleport)]
	[InlineData(HuntTrainPhase.Idle, HuntTrainEvent.StartMount, HuntTrainPhase.Mount)]
	[InlineData(HuntTrainPhase.Idle, HuntTrainEvent.StartNavigate, HuntTrainPhase.Navigate)]
	[InlineData(HuntTrainPhase.Mount, HuntTrainEvent.StartTeleport, HuntTrainPhase.Teleport)]
	[InlineData(HuntTrainPhase.Teleport, HuntTrainEvent.TeleportArrived, HuntTrainPhase.Navigate)]
	[InlineData(HuntTrainPhase.Mount, HuntTrainEvent.MountReady, HuntTrainPhase.Navigate)]
	[InlineData(HuntTrainPhase.Mount, HuntTrainEvent.EnterCombat, HuntTrainPhase.Combat)]
	[InlineData(HuntTrainPhase.Navigate, HuntTrainEvent.FlagArrived, HuntTrainPhase.Unmount)]
	[InlineData(HuntTrainPhase.Navigate, HuntTrainEvent.EnterCombat, HuntTrainPhase.Combat)]
	[InlineData(HuntTrainPhase.Unmount, HuntTrainEvent.EnterCombat, HuntTrainPhase.Combat)]
	[InlineData(HuntTrainPhase.Combat, HuntTrainEvent.CombatEnded, HuntTrainPhase.Idle)]
	public void Apply_legal_happy_path(HuntTrainPhase from, HuntTrainEvent ev, HuntTrainPhase expected)
	{
		Assert.True(HuntTrainTransition.TryGetNext(from, ev, out var to));
		Assert.Equal(expected, to);
		Assert.Equal(expected, HuntTrainTransition.Apply(from, ev));
	}

	[Theory]
	[InlineData(HuntTrainPhase.Teleport)]
	[InlineData(HuntTrainPhase.Mount)]
	[InlineData(HuntTrainPhase.Navigate)]
	[InlineData(HuntTrainPhase.Unmount)]
	[InlineData(HuntTrainPhase.Combat)]
	public void Abort_from_active_goes_Idle(HuntTrainPhase from)
	{
		Assert.True(HuntTrainTransition.TryGetNext(from, HuntTrainEvent.Abort, out var to));
		Assert.Equal(HuntTrainPhase.Idle, to);
		Assert.Equal(HuntTrainPhase.Idle, HuntTrainTransition.Apply(from, HuntTrainEvent.Abort));
	}

	[Fact]
	public void Abort_from_Idle_is_noop()
	{
		Assert.False(HuntTrainTransition.TryGetNext(HuntTrainPhase.Idle, HuntTrainEvent.Abort, out var to));
		Assert.Equal(HuntTrainPhase.Idle, to);
		Assert.Equal(HuntTrainPhase.Idle, HuntTrainTransition.Apply(HuntTrainPhase.Idle, HuntTrainEvent.Abort));
	}

	[Theory]
	[InlineData(HuntTrainPhase.Idle, HuntTrainEvent.None)]
	[InlineData(HuntTrainPhase.Idle, HuntTrainEvent.TeleportArrived)]
	[InlineData(HuntTrainPhase.Idle, HuntTrainEvent.MountReady)]
	[InlineData(HuntTrainPhase.Idle, HuntTrainEvent.FlagArrived)]
	[InlineData(HuntTrainPhase.Idle, HuntTrainEvent.ReadyForGroundFollow)]
	[InlineData(HuntTrainPhase.Idle, HuntTrainEvent.EnterCombat)]
	[InlineData(HuntTrainPhase.Idle, HuntTrainEvent.CombatEnded)]
	[InlineData(HuntTrainPhase.Teleport, HuntTrainEvent.StartTeleport)]
	[InlineData(HuntTrainPhase.Teleport, HuntTrainEvent.StartMount)]
	[InlineData(HuntTrainPhase.Teleport, HuntTrainEvent.MountReady)]
	[InlineData(HuntTrainPhase.Teleport, HuntTrainEvent.FlagArrived)]
	[InlineData(HuntTrainPhase.Mount, HuntTrainEvent.StartMount)]
	[InlineData(HuntTrainPhase.Mount, HuntTrainEvent.TeleportArrived)]
	[InlineData(HuntTrainPhase.Mount, HuntTrainEvent.FlagArrived)]
	[InlineData(HuntTrainPhase.Navigate, HuntTrainEvent.MountReady)]
	[InlineData(HuntTrainPhase.Navigate, HuntTrainEvent.ReadyForGroundFollow)]
	[InlineData(HuntTrainPhase.Navigate, HuntTrainEvent.StartTeleport)]
	[InlineData(HuntTrainPhase.Unmount, HuntTrainEvent.FlagArrived)]
	[InlineData(HuntTrainPhase.Unmount, HuntTrainEvent.ReadyForGroundFollow)]
	[InlineData(HuntTrainPhase.Combat, HuntTrainEvent.EnterCombat)]
	[InlineData(HuntTrainPhase.Combat, HuntTrainEvent.StartTeleport)]
	public void Apply_illegal_or_None_is_noop(HuntTrainPhase from, HuntTrainEvent ev)
	{
		Assert.False(HuntTrainTransition.TryGetNext(from, ev, out var to));
		Assert.Equal(from, to);
		Assert.Equal(from, HuntTrainTransition.Apply(from, ev));
	}

	[Fact]
	public void Full_pipeline_via_Apply_mount_before_tp()
	{
		var phase = HuntTrainPhase.Idle;
		phase = HuntTrainTransition.Apply(phase, HuntTrainEvent.StartMount);
		Assert.Equal(HuntTrainPhase.Mount, phase);
		phase = HuntTrainTransition.Apply(phase, HuntTrainEvent.StartTeleport);
		Assert.Equal(HuntTrainPhase.Teleport, phase);
		phase = HuntTrainTransition.Apply(phase, HuntTrainEvent.TeleportArrived);
		Assert.Equal(HuntTrainPhase.Navigate, phase);
		phase = HuntTrainTransition.Apply(phase, HuntTrainEvent.FlagArrived);
		Assert.Equal(HuntTrainPhase.Unmount, phase);
		phase = HuntTrainTransition.Apply(phase, HuntTrainEvent.EnterCombat);
		Assert.Equal(HuntTrainPhase.Combat, phase);
		phase = HuntTrainTransition.Apply(phase, HuntTrainEvent.CombatEnded);
		Assert.Equal(HuntTrainPhase.Idle, phase);
	}

	[Fact]
	public void Full_pipeline_via_Apply_already_mounted_tp()
	{
		var phase = HuntTrainPhase.Idle;
		phase = HuntTrainTransition.Apply(phase, HuntTrainEvent.StartTeleport);
		Assert.Equal(HuntTrainPhase.Teleport, phase);
		phase = HuntTrainTransition.Apply(phase, HuntTrainEvent.TeleportArrived);
		Assert.Equal(HuntTrainPhase.Navigate, phase);
	}

	[Fact]
	public void Decide_Idle_prefers_NeedsTeleport()
	{
		var snap = new HuntTrainTickSnapshot
		{
			PluginEnabled = true,
			NeedsTeleport = true,
			SameZoneReady = true,
			AlreadyMountedOrSkipMount = true,
		};
		Assert.Equal(HuntTrainEvent.StartTeleport, HuntTrainTransition.Decide(HuntTrainPhase.Idle, snap));
		Assert.Equal(HuntTrainPhase.Teleport, HuntTrainTransition.Tick(HuntTrainPhase.Idle, snap));
	}

	[Fact]
	public void Decide_Idle_prefers_AlreadyMounted_over_SameZoneReady()
	{
		var snap = new HuntTrainTickSnapshot
		{
			PluginEnabled = true,
			AlreadyMountedOrSkipMount = true,
			SameZoneReady = true,
		};
		Assert.Equal(HuntTrainEvent.StartNavigate, HuntTrainTransition.Decide(HuntTrainPhase.Idle, snap));
	}

	[Fact]
	public void Decide_Idle_SameZoneReady_starts_Mount()
	{
		var snap = new HuntTrainTickSnapshot
		{
			PluginEnabled = true,
			SameZoneReady = true,
		};
		Assert.Equal(HuntTrainEvent.StartMount, HuntTrainTransition.Decide(HuntTrainPhase.Idle, snap));
	}

	[Fact]
	public void Decide_Idle_no_signals_stays()
	{
		var snap = new HuntTrainTickSnapshot { PluginEnabled = true };
		Assert.Equal(HuntTrainEvent.None, HuntTrainTransition.Decide(HuntTrainPhase.Idle, snap));
		Assert.Equal(HuntTrainPhase.Idle, HuntTrainTransition.Tick(HuntTrainPhase.Idle, snap));
	}

	[Theory]
	[InlineData(HuntTrainPhase.Teleport, true, false, false, false, false, false, false, HuntTrainEvent.TeleportArrived, HuntTrainPhase.Navigate)]
	[InlineData(HuntTrainPhase.Mount, false, true, false, false, false, false, false, HuntTrainEvent.MountReady, HuntTrainPhase.Navigate)]
	[InlineData(HuntTrainPhase.Mount, false, true, false, false, false, false, true, HuntTrainEvent.StartTeleport, HuntTrainPhase.Teleport)]
	[InlineData(HuntTrainPhase.Navigate, false, false, true, false, false, false, false, HuntTrainEvent.FlagArrived, HuntTrainPhase.Unmount)]
	[InlineData(HuntTrainPhase.Unmount, false, false, false, true, true, false, false, HuntTrainEvent.EnterCombat, HuntTrainPhase.Combat)]
	[InlineData(HuntTrainPhase.Combat, false, false, false, false, false, true, false, HuntTrainEvent.CombatEnded, HuntTrainPhase.Idle)]
	public void Decide_and_Tick_progress_signals(
		HuntTrainPhase from,
		bool teleportComplete,
		bool mountComplete,
		bool withinArrival,
		bool readyFollow,
		bool partyEngaged,
		bool combatEnded,
		bool needsTeleport,
		HuntTrainEvent expectedEvent,
		HuntTrainPhase expectedPhase)
	{
		var snap = new HuntTrainTickSnapshot
		{
			PluginEnabled = true,
			TeleportComplete = teleportComplete,
			MountComplete = mountComplete,
			WithinFlagArrival = withinArrival,
			HuntTargetFound = withinArrival && expectedEvent == HuntTrainEvent.FlagArrived,
			ReadyForGroundFollow = readyFollow,
			PartyEngaged = partyEngaged,
			CombatEnded = combatEnded,
			NeedsTeleport = needsTeleport,
			// On foot so WithinFlagArrival + hunt target can FlagArrived (auto-unmount default).
			AutoUnmountAtFlag = true,
			MountedOrInFlight = false,
		};
		Assert.Equal(expectedEvent, HuntTrainTransition.Decide(from, snap));
		Assert.Equal(expectedPhase, HuntTrainTransition.Tick(from, snap));
	}

	[Fact]
	public void Decide_Unmount_ReadyForGroundFollow_alone_stays()
	{
		var snap = new HuntTrainTickSnapshot
		{
			PluginEnabled = true,
			ReadyForGroundFollow = true,
		};
		Assert.Equal(HuntTrainEvent.None, HuntTrainTransition.Decide(HuntTrainPhase.Unmount, snap));
		Assert.Equal(HuntTrainPhase.Unmount, HuntTrainTransition.Tick(HuntTrainPhase.Unmount, snap));
	}

	[Theory]
	[InlineData(HuntTrainPhase.Navigate)]
	[InlineData(HuntTrainPhase.Mount)]
	[InlineData(HuntTrainPhase.Unmount)]
	public void Decide_divert_PartyEngaged_enters_Combat(HuntTrainPhase from)
	{
		var snap = new HuntTrainTickSnapshot
		{
			PluginEnabled = true,
			PartyEngaged = true,
			MountComplete = true,
			WithinFlagArrival = true,
			ReadyForGroundFollow = true,
		};
		Assert.Equal(HuntTrainEvent.EnterCombat, HuntTrainTransition.Decide(from, snap));
		Assert.Equal(HuntTrainPhase.Combat, HuntTrainTransition.Tick(from, snap));
	}

	[Fact]
	public void Decide_Navigate_empty_flag_arrival_stays_Navigate()
	{
		var snap = new HuntTrainTickSnapshot
		{
			PluginEnabled = true,
			WithinFlagArrival = true,
			AutoUnmountAtFlag = true,
			MountedOrInFlight = false,
			HuntTargetFound = false,
		};
		Assert.Equal(HuntTrainEvent.None, HuntTrainTransition.Decide(HuntTrainPhase.Navigate, snap));
		Assert.Equal(HuntTrainPhase.Navigate, HuntTrainTransition.Tick(HuntTrainPhase.Navigate, snap));
	}

	[Fact]
	public void Decide_Navigate_arrival_while_mounted_stays_until_ReadyForGroundFollow()
	{
		var arrivedMounted = new HuntTrainTickSnapshot
		{
			PluginEnabled = true,
			WithinFlagArrival = true,
			AutoUnmountAtFlag = true,
			MountedOrInFlight = true,
			ReadyForGroundFollow = false,
			HuntTargetFound = true,
		};
		Assert.Equal(HuntTrainEvent.None, HuntTrainTransition.Decide(HuntTrainPhase.Navigate, arrivedMounted));
		Assert.Equal(HuntTrainPhase.Navigate, HuntTrainTransition.Tick(HuntTrainPhase.Navigate, arrivedMounted));

		var dismounted = arrivedMounted with { ReadyForGroundFollow = true };
		Assert.Equal(HuntTrainEvent.FlagArrived, HuntTrainTransition.Decide(HuntTrainPhase.Navigate, dismounted));
		Assert.Equal(HuntTrainPhase.Unmount, HuntTrainTransition.Tick(HuntTrainPhase.Navigate, dismounted));

		var noHuntReady = dismounted with { HuntTargetFound = false };
		Assert.Equal(HuntTrainEvent.None, HuntTrainTransition.Decide(HuntTrainPhase.Navigate, noHuntReady));
		Assert.Equal(HuntTrainPhase.Navigate, HuntTrainTransition.Tick(HuntTrainPhase.Navigate, noHuntReady));
	}

	[Fact]
	public void Decide_Navigate_PartyEngaged_beats_FlagArrived()
	{
		var snap = new HuntTrainTickSnapshot
		{
			PluginEnabled = true,
			PartyEngaged = true,
			WithinFlagArrival = true,
		};
		Assert.Equal(HuntTrainEvent.EnterCombat, HuntTrainTransition.Decide(HuntTrainPhase.Navigate, snap));
		Assert.Equal(HuntTrainPhase.Combat, HuntTrainTransition.Tick(HuntTrainPhase.Navigate, snap));
	}

	[Fact]
	public void Decide_master_off_aborts_active()
	{
		var snap = new HuntTrainTickSnapshot { PluginEnabled = false };
		Assert.Equal(HuntTrainEvent.Abort, HuntTrainTransition.Decide(HuntTrainPhase.Navigate, snap));
		Assert.Equal(HuntTrainPhase.Idle, HuntTrainTransition.Tick(HuntTrainPhase.Navigate, snap));
	}

	[Fact]
	public void Decide_master_off_Idle_is_noop()
	{
		var snap = new HuntTrainTickSnapshot { PluginEnabled = false, NeedsTeleport = true };
		Assert.Equal(HuntTrainEvent.None, HuntTrainTransition.Decide(HuntTrainPhase.Idle, snap));
		Assert.Equal(HuntTrainPhase.Idle, HuntTrainTransition.Tick(HuntTrainPhase.Idle, snap));
	}

	[Fact]
	public void Decide_Abort_flag_aborts_active()
	{
		var snap = new HuntTrainTickSnapshot { PluginEnabled = true, Abort = true };
		Assert.Equal(HuntTrainEvent.Abort, HuntTrainTransition.Decide(HuntTrainPhase.Unmount, snap));
		Assert.Equal(HuntTrainPhase.Idle, HuntTrainTransition.Tick(HuntTrainPhase.Unmount, snap));
	}

	[Fact]
	public void Decide_wrong_phase_signals_ignored()
	{
		// MountComplete while Teleporting must not jump ahead.
		var snap = new HuntTrainTickSnapshot
		{
			PluginEnabled = true,
			MountComplete = true,
			WithinFlagArrival = true,
			PartyEngaged = true,
		};
		Assert.Equal(HuntTrainEvent.None, HuntTrainTransition.Decide(HuntTrainPhase.Teleport, snap));
		Assert.Equal(HuntTrainPhase.Teleport, HuntTrainTransition.Tick(HuntTrainPhase.Teleport, snap));
	}

	[Fact]
	public void Default_snapshot_is_soft_noop()
	{
		HuntTrainTickSnapshot snap = default;
		Assert.Equal(HuntTrainEvent.None, HuntTrainTransition.Decide(HuntTrainPhase.Idle, snap));
		// PluginEnabled false on default → Abort when active.
		Assert.Equal(HuntTrainEvent.Abort, HuntTrainTransition.Decide(HuntTrainPhase.Combat, snap));
	}
}
