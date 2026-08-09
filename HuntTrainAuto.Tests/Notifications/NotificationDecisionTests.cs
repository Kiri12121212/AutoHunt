#nullable enable

namespace HuntTrainAuto.Tests.Notifications;

public sealed class NotificationDecisionTests
{
	[Theory]
	[InlineData(true, true, true)]
	[InlineData(true, false, false)]
	[InlineData(false, true, false)]
	[InlineData(false, false, false)]
	public void ShouldShowToast_requires_master_and_toggle(
		bool pluginEnabled,
		bool enableNotifications,
		bool expected)
		=> Assert.Equal(
			expected,
			NotificationDecision.ShouldShowToast(pluginEnabled, enableNotifications));

	[Theory]
	[InlineData(true, true, true)]
	[InlineData(true, false, false)]
	[InlineData(false, true, false)]
	[InlineData(false, false, false)]
	public void ShouldPlaySound_requires_master_and_toggle(
		bool pluginEnabled,
		bool enableSound,
		bool expected)
		=> Assert.Equal(
			expected,
			NotificationDecision.ShouldPlaySound(pluginEnabled, enableSound));

	[Fact]
	public void FormatContent_uses_place_name_when_present()
		=> Assert.Equal("Mor Dhona", NotificationDecision.FormatContent("  Mor Dhona  "));

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void FormatContent_falls_back_when_missing(string? placeName)
		=> Assert.Equal(NotificationDecision.DefaultContent, NotificationDecision.FormatContent(placeName));

	[Fact]
	public void FormatTitle_is_stable()
		=> Assert.Equal(NotificationDecision.DefaultTitle, NotificationDecision.FormatTitle());

	[Theory]
	[InlineData(true, "toast=show", "sound=play")]
	[InlineData(false, "toast=suppressed", "sound=suppressed")]
	public void Describe_reports_notification_outcomes(bool enabled, string toast, string sound)
	{
		Assert.Equal(toast, NotificationDecision.DescribeToast(enabled));
		Assert.Equal(sound, NotificationDecision.DescribeSound(enabled));
	}
}
