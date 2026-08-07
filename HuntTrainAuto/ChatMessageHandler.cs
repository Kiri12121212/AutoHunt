#nullable enable
using System;
using Dalamud.Game.Chat;
using Dalamud.Plugin.Services;

namespace HuntTrainAuto;

/// <summary>
/// Subscribes to <see cref="IChatGui.ChatMessage"/>. Cannot be unit-tested without Dalamud
/// (same constraint as Chat2Ipc); later tasks add sender decode, map links, highlight/suppress.
/// </summary>
public sealed class ChatMessageHandler : IDisposable
{
	private readonly IChatGui chatGui;

	public ChatMessageHandler(IChatGui chatGui)
	{
		this.chatGui = chatGui ?? throw new ArgumentNullException(nameof(chatGui));
		chatGui.ChatMessage += OnChatMessage;
	}

	private void OnChatMessage(IHandleableChatMessage message)
	{
		// Stub for phase-2 follow-ups (sender match, MapLinkPayload, highlight/suppress, open map).
		_ = message;
	}

	public void Dispose()
	{
		chatGui.ChatMessage -= OnChatMessage;
	}
}
