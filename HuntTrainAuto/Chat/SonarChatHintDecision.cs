#nullable enable
using System;

namespace HuntTrainAuto.Chat;

/// <summary>
/// Pure Sonar chat → engage-hint gate (HTA <c>SonarMonitor</c> parity).
/// <para>
/// Soft dependency: SonarPlugin exposes no public Dalamud IPC (CallGate / EzIPC
/// absent in 15.751.0.1). HuntAlerts already covers train coords via IPC; this
/// path only enriches the engage position hint when chat posts a map-link.
/// </para>
/// </summary>
public static class SonarChatHintDecision
{
	/// <summary>HTA parity: chat sender display name.</summary>
	public const string SenderName = "Sonar";

	/// <summary>
	/// Accept when sender is Sonar, message has a map-link, and is not a kill notice.
	/// </summary>
	public static bool ShouldRememberHint(
		string? senderName,
		string? messageText,
		bool hasMapLink)
	{
		if (!hasMapLink)
			return false;
		if (!string.Equals(senderName, SenderName, StringComparison.Ordinal))
			return false;

		var text = messageText ?? string.Empty;
		if (text.Contains("killed", StringComparison.OrdinalIgnoreCase))
			return false;

		return true;
	}
}
