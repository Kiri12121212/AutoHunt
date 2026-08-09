#nullable enable
using System;
using System.Linq;
using Dalamud.Game.Chat;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using HuntTrainAuto.Logging;

namespace HuntTrainAuto.Chat;

/// <summary>
/// Subscribes to <see cref="IChatGui.ChatMessage"/>. Sender match, map-link → <see cref="HuntFlag"/>,
/// optional auto-open map with AgentMap dedupe, and conductor highlight.
/// Teleport decision is computed only (stored on <see cref="TeleportIntent"/>) — no Teleporter/Lifestream calls.
/// </summary>
public sealed class ChatMessageHandler : IDisposable
{
	/// <summary>HTA parity UI foreground color for conductor chat lines.</summary>
	public const ushort ConductorUiForeground = 578;

	private readonly IChatGui chatGui;
	private readonly IGameGui gameGui;
	private readonly Configuration config;
	private readonly IPluginLog? log;

	public ChatMessageHandler(
		IChatGui chatGui,
		IGameGui gameGui,
		Configuration config,
		IPluginLog? log = null)
	{
		this.chatGui = chatGui ?? throw new ArgumentNullException(nameof(chatGui));
		this.gameGui = gameGui ?? throw new ArgumentNullException(nameof(gameGui));
		this.config = config ?? throw new ArgumentNullException(nameof(config));
		this.log = log;
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

	/// <summary>Raised for every conductor chat line (map-link or plain text).</summary>
	public event Action<string>? ConductorTextReceived;

	private void OnChatMessage(IHandleableChatMessage message)
	{
		IsConductorMessage = false;
		ConductorSenderName = null;

		if (!config.Enabled)
			return;

		var isConductorMessage = TryDecodeSender(message.Sender, out var senderName)
			&& ChatSender.IsConductor(config.Conductors, senderName);
		if (!isConductorMessage)
			return;

		LogDebug("conductor sender matched");
		IsConductorMessage = true;
		ConductorSenderName = senderName;
		TryExtractHuntFlag(message);
		var isLastStop = ConductorLastStopParse.IsLastStop(message.Message.TextValue);
		LogDebug(ConductorLastStopParse.Describe(isLastStop));
		try
		{
			ConductorTextReceived?.Invoke(message.Message.TextValue);
		}
		catch
		{
			// soft-fail subscriber
			LogDebug("conductor text subscriber soft-fail");
		}

		message.Message = HighlightConductorMessage(message.Message);
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

			var instanceHint = ConductorInstanceParse.TryParse(message.Message.TextValue);
			flag.ReportedInstance = instanceHint;
			LogDebug($"flag accepted: territory={flag.TerritoryTypeId}, {ConductorInstanceParse.Describe(instanceHint)}");
			LatestHuntFlag = flag;
			TryEvaluateTeleportDecision(flag, instanceHint);
			try
			{
				HuntFlagReceived?.Invoke(flag);
			}
			catch
			{
				LogDebug("flag subscriber soft-fail");
			}

			if (config.AutoOpenMap
				&& MapOpenDedupe.IsPlausibleMapLink(flag.TerritoryTypeId, flag.MapId, flag.RawX, flag.RawY)
				&& ShouldOpenForFreshLink(flag))
			{
				gameGui.OpenMapWithMapLink(mapLink);
				LogDebug("map opened for fresh flag");
			}

			return;
		}

		LogDebug("flag rejected: no map link");
	}

	/// <summary>
	/// Computes teleport decision and stores it on <see cref="TeleportIntent"/>.
	/// Soft-fails when snapshot unavailable or provider throws — never teleports.
	/// <paramref name="targetInstanceHint"/> is conductor/flag-reported instance (0 = none).
	/// </summary>
	private void TryEvaluateTeleportDecision(HuntFlag flag, int targetInstanceHint = 0)
	{
		try
		{
			var snapshot = TryGetPlayerSnapshot?.Invoke(flag);
			if (snapshot is { } s && targetInstanceHint > 0)
				snapshot = s with { TargetInstance = targetInstanceHint };

			var decision = TeleportDecision.Evaluate(
				config.Enabled,
				config.AutoTeleport,
				config.AutoTeleportAetheryteDistanceDiff,
				flag,
				snapshot,
				CreateTimeAwareSettings(config));
			TeleportIntent.Set(decision);
			LogDebug($"teleport decision: {decision.Describe()}");
		}
		catch
		{
			TeleportIntent.Set(new TeleportDecisionResult
			{
				Action = TeleportAction.Skip,
				SkipReason = TeleportSkipReason.PlayerStateUnavailable,
			});
			LogDebug("teleport decision: action=Skip, skip=PlayerStateUnavailable");
		}
	}

	private void LogDebug(string message)
	{
		if (log != null)
			DebugBehavior.Debug(log, config.EnableDebugLogging, "Chat", message);
	}

	/// <summary>Dedupe against live AgentMap flag; acts on the fresh extract, not stale state alone.</summary>
	private bool ShouldOpenForFreshLink(HuntFlag flag)
	{
		var hasFlag = AgentMapFlag.TryGet(out var territoryId, out var x, out var y);
		return MapOpenDedupe.ShouldOpenMap(
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

	internal static SameZoneTimeAwareSettings CreateTimeAwareSettings(Configuration cfg)
		=> SameZoneTravelCost.CreateSettings(
			cfg.AutoTeleportCastSeconds,
			cfg.AutoTeleportLoadEstimateSeconds,
			cfg.AutoTeleportMountSpeedYalmsPerSec,
			cfg.AutoTeleportMountUpSeconds,
			cfg.AutoTeleportRetainDistanceFloor,
			cfg.TeleportDelayEnabled,
			cfg.TeleportDelayMin,
			cfg.TeleportDelayMax);

	public void Dispose()
	{
		chatGui.ChatMessage -= OnChatMessage;
	}
}
