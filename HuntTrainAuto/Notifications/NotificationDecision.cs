#nullable enable

namespace HuntTrainAuto.Notifications;

/// <summary>
/// Pure gates + copy for conductor-flag notifications (TASKS 9.1).
/// </summary>
public static class NotificationDecision
{
	public const string DefaultTitle = "HuntTrainAuto";
	public const string DefaultContent = "Conductor flag received";

	/// <summary>Dalamud toast when master + notifications are on.</summary>
	public static bool ShouldShowToast(bool pluginEnabled, bool enableNotifications)
		=> pluginEnabled && enableNotifications;

	/// <summary>Optional audio when master + sound toggle are on (independent of toast).</summary>
	public static bool ShouldPlaySound(bool pluginEnabled, bool enableSound)
		=> pluginEnabled && enableSound;

	/// <summary>Compact, side-effect-free toast diagnostic for call-site logging.</summary>
	public static string DescribeToast(bool shouldShowToast)
		=> shouldShowToast ? "toast=show" : "toast=suppressed";

	/// <summary>Compact, side-effect-free sound diagnostic for call-site logging.</summary>
	public static string DescribeSound(bool shouldPlaySound)
		=> shouldPlaySound ? "sound=play" : "sound=suppressed";

	public static string FormatTitle() => DefaultTitle;

	/// <summary>Prefer place name; fall back to generic content.</summary>
	public static string FormatContent(string? placeName)
	{
		var trimmed = placeName?.Trim();
		return string.IsNullOrEmpty(trimmed) ? DefaultContent : trimmed;
	}
}
