#nullable enable

namespace HuntTrainAuto.Tests.State;

public sealed class HuntTrainControllerTests
{
	[Fact]
	public void Starts_Idle_inactive()
	{
		var c = new HuntTrainController();
		Assert.Equal(HuntTrainPhase.Idle, c.Phase);
		Assert.False(c.IsActive);
	}

	[Fact]
	public void Apply_advances_and_illegal_stays()
	{
		var c = new HuntTrainController();
		Assert.Equal(HuntTrainPhase.Teleport, c.Apply(HuntTrainEvent.StartTeleport));
		Assert.True(c.IsActive);
		Assert.Equal(HuntTrainPhase.Teleport, c.Apply(HuntTrainEvent.MountReady)); // illegal
		Assert.Equal(HuntTrainPhase.Mount, c.Apply(HuntTrainEvent.TeleportArrived));
	}

	[Fact]
	public void Tick_drives_from_snapshot()
	{
		var c = new HuntTrainController();
		c.Tick(new HuntTrainTickSnapshot { PluginEnabled = true, SameZoneReady = true });
		Assert.Equal(HuntTrainPhase.Mount, c.Phase);
		c.Tick(new HuntTrainTickSnapshot { PluginEnabled = true, MountComplete = true });
		Assert.Equal(HuntTrainPhase.Navigate, c.Phase);
	}

	[Fact]
	public void Reset_and_Clear_force_Idle()
	{
		var c = new HuntTrainController();
		c.Apply(HuntTrainEvent.StartNavigate);
		Assert.Equal(HuntTrainPhase.Navigate, c.Phase);
		c.Reset();
		Assert.Equal(HuntTrainPhase.Idle, c.Phase);
		Assert.False(c.IsActive);

		c.Apply(HuntTrainEvent.StartMount);
		c.Clear();
		Assert.Equal(HuntTrainPhase.Idle, c.Phase);
	}

	[Fact]
	public void Apply_Abort_resets_to_Idle()
	{
		var c = new HuntTrainController();
		c.Apply(HuntTrainEvent.StartTeleport);
		c.Apply(HuntTrainEvent.TeleportArrived);
		c.Apply(HuntTrainEvent.MountReady);
		Assert.Equal(HuntTrainPhase.Navigate, c.Phase);
		Assert.Equal(HuntTrainPhase.Idle, c.Apply(HuntTrainEvent.Abort));
	}

	[Fact]
	public void Tick_master_off_resets()
	{
		var c = new HuntTrainController();
		c.Apply(HuntTrainEvent.StartMount);
		c.Tick(new HuntTrainTickSnapshot { PluginEnabled = false });
		Assert.Equal(HuntTrainPhase.Idle, c.Phase);
	}
}
