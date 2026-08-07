#nullable enable
using System;
using System.Linq;
using Dalamud.Game.Chat;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;

namespace HuntTrainAuto;

/// <summary>
/// Subscribes to <see cref="IChatGui.ChatMessage"/>. Sender match + map-link → <see cref="HuntFlag"/>
/// extraction are wired here; highlight / suppress / open-map remain for later phase-2 tasks.
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
	/// Result of the most recent chat message evaluation. Later tasks (highlight, suppress)
	/// can read this without re-decoding. Map-link work must run in the same callback pass.
	/// </summary>
	public bool IsConductorMessage { get; private set; }

	/// <summary>Decoded sender name when <see cref="IsConductorMessage"/> is true; otherwise null.</summary>
	public string? ConductorSenderName { get; private set; }

	/// <summary>Most recently extracted conductor map-link flag, if any.</summary>
	public HuntFlag? LatestHuntFlag { get; private set; }

	/// <summary>Raised when a conductor message yields a new <see cref="HuntFlag"/>.</summary>
	public event Action<HuntFlag>? HuntFlagReceived;

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

		TryExtractHuntFlag(message);

		// Stub for phase-2 follow-ups (highlight/suppress, open map).
	}

	/// <summary>
	/// First <see cref="MapLinkPayload"/> in a conductor message becomes <see cref="LatestHuntFlag"/>.
	/// Does not open the map or dedupe AgentMap markers.
	/// </summary>
	private void TryExtractHuntFlag(IHandleableChatMessage message)
	{
		foreach (var payload in message.Message.Payloads)
		{
			if (payload is not MapLinkPayload mapLink)
				continue;

			var flag = HuntFlag.FromMapLink(
				mapLink.TerritoryType.RowId,
				mapLink.Map.RowId,
				mapLink.RawX,
				mapLink.RawY,
				mapLink.PlaceName);

			LatestHuntFlag = flag;
			HuntFlagReceived?.Invoke(flag);
			return;
		}
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
