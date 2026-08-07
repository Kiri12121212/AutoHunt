#nullable enable
using System;
using System.Linq;
using Dalamud.Game.Chat;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;

namespace HuntTrainAuto.Chat;

/// <summary>
/// Subscribes to <see cref="IChatGui.ChatMessage"/>. Sender match, map-link → <see cref="HuntFlag"/>,
/// optional auto-open map with AgentMap dedupe, conductor highlight, and optional non-conductor suppress.
/// Teleport decision is computed only (stored on <see cref="TeleportIntent"/>) — no Teleporter/Lifestream calls.
/// </summary>
public sealed class ChatMessageHandler : IDisposable
{
	/// <summary>HTA parity UI foreground color for conductor chat lines.</summary>
	public const ushort ConductorUiForeground = 578;

	private readonly IChatGui chatGui;
	private readonly IGameGui gameGui;
	private readonly Configuration config;

	public ChatMessageHandler(IChatGui chatGui, IGameGui gameGui, Configuration config)
	{
		this.chatGui = chatGui ?? throw new ArgumentNullException(nameof(chatGui));
		this.gameGui = gameGui ?? throw new ArgumentNullException(nameof(gameGui));
		this.config = config ?? throw new ArgumentNullException(nameof(config));
		chatGui.ChatMessage += OnChatMessage;
	}

	/// <summary>
	/// Result of the most recent chat message evaluation. Highlight and map-link work must run in the same callback pass.
	/// </summary>
	public bool IsConductorMessage { get; private set; }

	/// <summary>Decoded sender name when <see cref="IsConductorMessage"/> is true; otherwise null.</summary>
	public string? ConductorSenderName { get; private set; }

	/// <summary>Most recently extracted conductor map-link flag, if any.</summary>
	public HuntFlag? LatestHuntFlag { get; private set; }

	/// <summary>Latest teleport decision / intended arrival (never executes TP).</summary>
	public TeleportIntent TeleportIntent { get; } = new();

	/// <summary>
	/// Optional player/nearest snapshot for decision evaluation.
	/// Return null to soft-fail (records <see cref="TeleportSkipReason.PlayerStateUnavailable"/>).
	/// </summary>
	public Func<HuntFlag, TeleportPlayerSnapshot?>? TryGetPlayerSnapshot { get; set; }

	/// <summary>Raised when a conductor message yields a new <see cref="HuntFlag"/>.</summary>
	public event Action<HuntFlag>? HuntFlagReceived;

	private void OnChatMessage(IHandleableChatMessage message)
	{
		IsConductorMessage = false;
		ConductorSenderName = null;

		if (!config.Enabled)
			return;

		var isConductorMessage = TryDecodeSender(message.Sender, out var senderName)
			&& ChatSender.IsConductor(config.Conductors, senderName);
		var isMapLink = ContainsMapLink(message.Message);

		if (isConductorMessage)
		{
			IsConductorMessage = true;
			ConductorSenderName = senderName;
			TryExtractHuntFlag(message);
			message.Message = HighlightConductorMessage(message.Message);
		}

		if (ChatSuppress.ShouldSuppress(
			config.SuppressChatOtherPlayers,
			isMapLink,
			isConductorMessage,
			config.Conductors.Count))
		{
			message.PreventOriginal();
		}
	}

	/// <summary>
	/// Wrap existing payloads with UI foreground <see cref="ConductorUiForeground"/> (HTA parity).
	/// Uses Dalamud <see cref="SeStringBuilder"/> — not linked into pure unit tests.
	/// </summary>
	internal static SeString HighlightConductorMessage(SeString message)
	{
		ArgumentNullException.ThrowIfNull(message);

		var builder = new SeStringBuilder();
		builder.AddUiForeground(ConductorUiForeground);
		foreach (var payload in message.Payloads)
			builder.Add(payload);
		builder.AddUiForegroundOff();
		return builder.Build();
	}

	/// <summary>True when the message contains any <see cref="MapLinkPayload"/> (HTA <c>isMapLink</c>).</summary>
	internal static bool ContainsMapLink(SeString message)
	{
		ArgumentNullException.ThrowIfNull(message);

		foreach (var payload in message.Payloads)
		{
			if (payload is MapLinkPayload)
				return true;
		}

		return false;
	}

	/// <summary>
	/// First <see cref="MapLinkPayload"/> in a conductor message becomes <see cref="LatestHuntFlag"/>;
	/// when <see cref="Configuration.AutoOpenMap"/>, may call <see cref="IGameGui.OpenMapWithMapLink"/>.
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
			TryEvaluateTeleportDecision(flag);
			HuntFlagReceived?.Invoke(flag);

			if (config.AutoOpenMap
				&& MapOpenDedupe.IsPlausibleMapLink(flag.TerritoryTypeId, flag.MapId, flag.RawX, flag.RawY)
				&& ShouldOpenForFreshLink(flag))
			{
				gameGui.OpenMapWithMapLink(mapLink);
			}

			return;
		}
	}

	/// <summary>
	/// Computes teleport decision and stores it on <see cref="TeleportIntent"/>.
	/// Soft-fails when snapshot unavailable or provider throws — never teleports.
	/// </summary>
	private void TryEvaluateTeleportDecision(HuntFlag flag)
	{
		try
		{
			var snapshot = TryGetPlayerSnapshot?.Invoke(flag);
			var decision = TeleportDecision.Evaluate(
				config.Enabled,
				config.AutoTeleport,
				config.AutoTeleportAetheryteDistanceDiff,
				config.AutoSwitchInstanceToOne,
				flag,
				snapshot);
			TeleportIntent.Set(decision);
		}
		catch
		{
			TeleportIntent.Set(new TeleportDecisionResult
			{
				Action = TeleportAction.Skip,
				SkipReason = TeleportSkipReason.PlayerStateUnavailable,
			});
		}
	}

	/// <summary>Dedupe against live AgentMap flag; acts on the fresh extract, not stale state alone.</summary>
	private bool ShouldOpenForFreshLink(HuntFlag flag)
	{
		var hasFlag = AgentMapFlag.TryGet(out var territoryId, out var x, out var y);
		return MapOpenDedupe.ShouldOpenMap(
			config.NoDuplicateFlags,
			hasFlag,
			territoryId,
			x,
			y,
			flag.TerritoryTypeId,
			flag.RawX,
			flag.RawY);
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
