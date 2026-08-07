#nullable enable
using System;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Plugin.Services;

namespace HuntTrainAuto.Notifications;

/// <summary>
/// Soft-fail conductor-flag toast / optional UI sound (TASKS 9.1 / 9.3).
/// Never throws to chat / Framework callers.
/// </summary>
public sealed class HuntNotificator
{
	private readonly INotificationManager? notificationManager;
	private readonly IPluginLog pluginLog;
	private readonly Func<bool> isPluginEnabled;
	private readonly Func<bool> enableNotifications;
	private readonly Func<bool> enableSound;
	private readonly Action? playSound;

	public HuntNotificator(
		INotificationManager? notificationManager,
		IPluginLog pluginLog,
		Func<bool> isPluginEnabled,
		Func<bool> enableNotifications,
		Func<bool> enableSound,
		Action? playSound = null)
	{
		this.notificationManager = notificationManager;
		this.pluginLog = pluginLog;
		this.isPluginEnabled = isPluginEnabled;
		this.enableNotifications = enableNotifications;
		this.enableSound = enableSound;
		this.playSound = playSound;
	}

	/// <summary>Notify on a conductor hunt flag. Soft-fails if manager / sound unavailable.</summary>
	public void NotifyConductorFlag(HuntFlag flag)
	{
		try
		{
			var enabled = SafeBool(isPluginEnabled);
			var toast = NotificationDecision.ShouldShowToast(enabled, SafeBool(enableNotifications));
			var sound = NotificationDecision.ShouldPlaySound(enabled, SafeBool(enableSound));

			if (toast)
				TryShowToast(NotificationDecision.FormatTitle(), NotificationDecision.FormatContent(flag.PlaceName));

			if (sound)
				TryPlaySound();
		}
		catch (Exception ex)
		{
			pluginLog.Debug($"HuntNotificator soft-fail: {ex.Message}");
		}
	}

	private void TryShowToast(string title, string content)
	{
		try
		{
			if (notificationManager == null)
				return;

			notificationManager.AddNotification(new Notification
			{
				Title = title,
				Content = content,
				Type = NotificationType.Info,
			});
		}
		catch (Exception ex)
		{
			pluginLog.Debug($"Notification toast soft-fail: {ex.Message}");
		}
	}

	private void TryPlaySound()
	{
		try
		{
			playSound?.Invoke();
		}
		catch (Exception ex)
		{
			pluginLog.Debug($"Notification sound soft-fail: {ex.Message}");
		}
	}

	private static bool SafeBool(Func<bool> probe)
	{
		try
		{
			return probe();
		}
		catch
		{
			return false;
		}
	}
}
