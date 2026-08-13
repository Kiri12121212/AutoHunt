#nullable enable
using System;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Plugin.Services;
using HuntTrainAuto.Logging;

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
	private readonly Func<bool> isDebugEnabled;

	public HuntNotificator(
		INotificationManager? notificationManager,
		IPluginLog pluginLog,
		Func<bool> isPluginEnabled,
		Func<bool> enableNotifications,
		Func<bool> enableSound,
		Action? playSound = null,
		Func<bool>? isDebugEnabled = null)
	{
		this.notificationManager = notificationManager;
		this.pluginLog = pluginLog;
		this.isPluginEnabled = isPluginEnabled;
		this.enableNotifications = enableNotifications;
		this.enableSound = enableSound;
		this.playSound = playSound;
		this.isDebugEnabled = isDebugEnabled ?? (() => false);
	}

	/// <summary>Play HuntAlerts parse-success sound. Soft-fails if handler unavailable.</summary>
	public void NotifyHuntAlertParsed(bool mappedSuccessfully = true)
	{
		try
		{
			if (!NotificationDecision.ShouldPlayHuntAlertSound(mappedSuccessfully))
				return;

			TryPlaySound();
		}
		catch (Exception ex)
		{
			LogDebug($"hunt-alert sound soft-fail: {ex.Message}");
		}
	}

	/// <summary>Notify on a conductor hunt flag. Soft-fails if manager / sound unavailable.</summary>
	public void NotifyConductorFlag(HuntFlag flag)
	{
		try
		{
			var enabled = SafeBool(isPluginEnabled);
			var notificationsEnabled = SafeBool(enableNotifications);
			var soundEnabled = SafeBool(enableSound);
			var toast = NotificationDecision.ShouldShowToast(enabled, notificationsEnabled);
			var sound = NotificationDecision.ShouldPlaySound(enabled, soundEnabled);

			if (toast)
				TryShowToast(NotificationDecision.FormatTitle(), NotificationDecision.FormatContent(flag.PlaceName));
			else
				LogDebug($"{NotificationDecision.DescribeToast(toast)}: plugin={enabled}, toggle={notificationsEnabled}");

			if (sound)
				TryPlaySound();
			else
				LogDebug($"{NotificationDecision.DescribeSound(sound)}: plugin={enabled}, toggle={soundEnabled}");
		}
		catch (Exception ex)
		{
			LogDebug($"notify soft-fail: {ex.Message}");
		}
	}

	private void TryShowToast(string title, string content)
	{
		try
		{
			if (notificationManager == null)
			{
				LogDebug("toast suppressed: notification manager unavailable");
				return;
			}

			notificationManager.AddNotification(new Notification
			{
				Title = title,
				Content = content,
				Type = NotificationType.Info,
			});
		}
		catch (Exception ex)
		{
			LogDebug($"toast soft-fail: {ex.Message}");
		}
	}

	private void TryPlaySound()
	{
		try
		{
			if (playSound == null)
			{
				LogDebug("sound suppressed: handler unavailable");
				return;
			}

			playSound();
		}
		catch (Exception ex)
		{
			LogDebug($"sound soft-fail: {ex.Message}");
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

	private void LogDebug(string message)
	{
		if (IsDebugEnabled())
			DebugBehavior.Debug(pluginLog, enabled: true, "Notify", message);
	}

	private bool IsDebugEnabled()
	{
		try
		{
			return isDebugEnabled();
		}
		catch
		{
			return false;
		}
	}
}
