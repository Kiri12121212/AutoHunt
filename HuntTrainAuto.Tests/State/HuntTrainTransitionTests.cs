#nullable enable

namespace HuntTrainAuto.Tests.State;

public sealed class HuntTrainTransitionTests
{
	[Theory]
	[InlineData(HuntTrainPhase.Idle, HuntTrainEvent.StartTeleport, HuntTrainPhase.Teleport)]
	[InlineData(HuntTrainPhase.Idle, HuntTrainEvent.StartMount, HuntTrainPhase.Mount)]
	[InlineData(HuntTrainPhase.Idle, HuntTrainEvent.StartNavigate, HuntTrainPhase.Navigate)]
	[InlineData(HuntTrainPhase.Teleport, HuntTrainEvent.TeleportArrived, HuntTrainPhase.Mount)]
	[InlineData(HuntTrainPhase.Mount, HuntTrainEvent.MountReady, HuntTrainPhase.Navigate)]
	[InlineData(HuntTrainPhase.Navigate, HuntTrainEvent.FlagArrived, HuntTrainPhase.Unmount)]
	[InlineData(HuntTrainPhase.Unmount, HuntTrainEvent.ReadyForGroundFollow, HuntTrainPhase.FollowParty)]
	[InlineData(HuntTrainPhase.FollowParty, HuntTrainEvent.EnterCombat, HuntTrainPhase.Combat)]
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
	[InlineData(HuntTrainPhase.FollowParty)]
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
	[InlineData(HuntTrainPhase.Unmount, HuntTrainEvent.FlagArrived)]
	[InlineData(HuntTrainPhase.Unmount, HuntTrainEvent.EnterCombat)]
	[InlineData(HuntTrainPhase.FollowParty, HuntTrainEvent.ReadyForGroundFollow)]
	[InlineData(HuntTrainPhase.FollowParty, HuntTrainEvent.CombatEnded)]
	[InlineData(HuntTrainPhase.Combat, HuntTrainEvent.EnterCombat)]
	[InlineData(HuntTrainPhase.Combat, HuntTrainEvent.StartTeleport)]
	public void Apply_illegal_or_None_is_noop(HuntTrainPhase from, HuntTrainEvent ev)
	{
		Assert.False(HuntTrainTransition.TryGetNext(from, ev, out var to));
		Assert.Equal(from, to);
		Assert.Equal(from, HuntTrainTransition.Apply(from, ev));
	}

	[Fact]
	public void Full_pipeline_via_Apply()
	{
		var phase = HuntTrainPhase.Idle;
		phase = HuntTrainTransition.Apply(phase, HuntTrainEvent.StartTeleport);
		Assert.Equal(HuntTrainPhase.Teleport, phase);
		phase = HuntTrainTransition.Apply(phase, HuntTrainEvent.TeleportArrived);
		Assert.Equal(HuntTrainPhase.Mount, phase);
		phase = HuntTrainTransition.Apply(phase, HuntTrainEvent.MountReady);
		Assert.Equal(HuntTrainPhase.Navigate, phase);
		phase = HuntTrainTransition.Apply(phase, HuntTrainEvent.FlagArrived);
		Assert.Equal(HuntTrainPhase.Unmount, phase);
		phase = HuntTrainTransition.Apply(phase, HuntTrainEvent.ReadyForGroundFollow);
		Assert.Equal(HuntTrainPhase.FollowParty, phase);
		phase = HuntTrainTransition.Apply(phase, HuntTrainEvent.EnterCombat);
		Assert.Equal(HuntTrainPhase.Combat, phase);
		phase = HuntTrainTransition.Apply(phase, HuntTrainEvent.CombatEnded);
		Assert.Equal(HuntTrainPhase.Idle, phase);
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
	[InlineData(HuntTrainPhase.Teleport, true, false, false, false, false, false, HuntTrainEvent.TeleportArrived, HuntTrainPhase.Mount)]
	[InlineData(HuntTrainPhase.Mount, false, true, false, false, false, false, HuntTrainEvent.MountReady, HuntTrainPhase.Navigate)]
	[InlineData(HuntTrainPhase.Navigate, false, false, true, false, false, false, HuntTrainEvent.FlagArrived, HuntTrainPhase.Unmount)]
	[InlineData(HuntTrainPhase.Unmount, false, false, false, true, false, false, HuntTrainEvent.ReadyForGroundFollow, HuntTrainPhase.FollowParty)]
	[InlineData(HuntTrainPhase.FollowParty, false, false, false, false, true, false, HuntTrainEvent.EnterCombat, HuntTrainPhase.Combat)]
	[InlineData(HuntTrainPhase.Combat, false, false, false, false, false, true, HuntTrainEvent.CombatEnded, HuntTrainPhase.Idle)]
	public void Decide_and_Tick_progress_signals(
		HuntTrainPhase from,
		bool teleportComplete,
		bool mountComplete,
		bool withinArrival,
		bool readyFollow,
		bool partyEngaged,
		bool combatEnded,
		HuntTrainEvent expectedEvent,
		HuntTrainPhase expectedPhase)
	{
		var snap = new HuntTrainTickSnapshot
		{
			PluginEnabled = true,
			TeleportComplete = teleportComplete,
			MountComplete = mountComplete,
			WithinFlagArrival = withinArrival,
			ReadyForGroundFollow = readyFollow,
			PartyEngaged = partyEngaged,
			CombatEnded = combatEnded,
		};
		Assert.Equal(expectedEvent, HuntTrainTransition.Decide(from, snap));
		Assert.Equal(expectedPhase, HuntTrainTransition.Tick(from, snap));
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
		Assert.Equal(HuntTrainEvent.Abort, HuntTrainTransition.Decide(HuntTrainPhase.FollowParty, snap));
		Assert.Equal(HuntTrainPhase.Idle, HuntTrainTransition.Tick(HuntTrainPhase.FollowParty, snap));
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
