#nullable enable

namespace HuntTrainAuto.Tests.Logging;

public sealed class DebugEventFormatterTests
{
	[Theory]
	[InlineData(true, true)]
	[InlineData(false, false)]
	public void ShouldRecord_mirrors_toggle(bool enabled, bool expected)
		=> Assert.Equal(expected, DebugEventFormatter.ShouldRecord(enabled));

	[Fact]
	public void FormatPhaseChange_uses_status_labels()
		=> Assert.Equal(
			"Idle → Teleport",
			DebugEventFormatter.FormatPhaseChange(HuntTrainPhase.Idle, HuntTrainPhase.Teleport));

	[Fact]
	public void FormatFlagReceived_prefers_place_name()
		=> Assert.Equal(
			"Conductor flag: Labyrinthos",
			DebugEventFormatter.FormatFlagReceived(" Labyrinthos "));

	[Fact]
	public void FormatFlagReceived_falls_back()
		=> Assert.Equal(
			"Conductor flag received",
			DebugEventFormatter.FormatFlagReceived(null));

	[Fact]
	public void FormatMountChange_and_unmount_change()
	{
		Assert.Equal(
			"idle → mounting",
			DebugEventFormatter.FormatMountChange(MountPhase.Idle, MountPhase.Mounting));
		Assert.Equal(
			"wait ready → unmounting",
			DebugEventFormatter.FormatUnmountChange(UnmountPhase.WaitReady, UnmountPhase.Unmounting));
	}

	[Fact]
	public void FormatLine_includes_kind_and_message()
	{
		var e = new DebugEvent
		{
			Timestamp = new(2026, 8, 7, 21, 5, 9, System.TimeSpan.Zero),
			Kind = DebugEventKind.PhaseChange,
			Message = "Idle → Mount",
		};
		Assert.Equal("[21:05:09] State: Idle → Mount", DebugEventFormatter.FormatLine(e));
	}
}
