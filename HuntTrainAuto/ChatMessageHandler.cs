#nullable enable
using System;
using System.Linq;
using Dalamud.Game.Chat;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;

namespace HuntTrainAuto;

/// <summary>
/// Subscribes to <see cref="IChatGui.ChatMessage"/>. Sender decode/match is wired here;
/// map-link / highlight / suppress / open-map remain for later phase-2 tasks.
/// </summary>
public sealed class ChatMessageHandler : IDisposable
{
	private readonly IChatGui chatGui;
	private readonly Configuration config;

	public ChatMessageHandler(IChatGui chatGui, Configuration config)
	{
		this.chatGui = chatGui ?? throw new ArgumentNullException(nameof(chatGui));
		this.config = config ?? throw new ArgumentNullException(nameof(config));
		chatGui.ChatMessage += OnChatMessage;
	}

	/// <summary>
	/// Result of the most recent chat message evaluation. Later tasks (map link, highlight,
	/// suppress) can read this without re-decoding.
	/// </summary>
	public bool IsConductorMessage { get; private set; }

	/// <summary>Decoded sender name when <see cref="IsConductorMessage"/> is true; otherwise null.</summary>
	public string? ConductorSenderName { get; private set; }

	private void OnChatMessage(IHandleableChatMessage message)
	{
		IsConductorMessage = false;
		ConductorSenderName = null;

		if (!config.Enabled)
			return;

		if (!TryDecodeSender(message.Sender, out var senderName))
			return;

		if (!ChatSender.IsConductor(config.Conductors, senderName))
			return;

		IsConductorMessage = true;
		ConductorSenderName = senderName;

		// Stub for phase-2 follow-ups (MapLinkPayload, highlight/suppress, open map).
	}

	/// <summary>
	/// HTA-style sender decode: prefer <see cref="PlayerPayload"/>, then careful text fallback.
	/// </summary>
	internal static bool TryDecodeSender(SeString? sender, out string playerName)
	{
		if (sender == null)
		{
			playerName = null!;
			return false;
		}

		var payloadNames = sender.Payloads.OfType<PlayerPayload>().Select(static p => p.PlayerName);
		return ChatSender.TryDecode(payloadNames, sender.TextValue, out playerName);
	}

	public void Dispose()
	{
		chatGui.ChatMessage -= OnChatMessage;
	}
}
