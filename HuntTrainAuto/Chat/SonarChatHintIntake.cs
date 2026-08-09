#nullable enable
using System;
using Dalamud.Game.Chat;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using HuntTrainAuto.Combat;
using HuntTrainAuto.Domain;

namespace HuntTrainAuto.Chat;

/// <summary>
/// Soft Sonar chat → <see cref="EngagePositionHint"/>.
/// Sonar is a soft dependency (no CallGate/EzIPC on SonarPlugin 15.x); absent
/// plugin simply means no "Sonar" sender lines. HuntAlerts / conductor flags
/// still feed the same hint slot.
/// </summary>
public sealed class SonarChatHintIntake : IDisposable
{
	private readonly IChatGui chatGui;
	private readonly Configuration config;
	private readonly EngagePositionHint hintSlot;
	private readonly IPluginLog? log;

	public SonarChatHintIntake(
		IChatGui chatGui,
		Configuration config,
		EngagePositionHint hintSlot,
		IPluginLog? log = null)
	{
		this.chatGui = chatGui ?? throw new ArgumentNullException(nameof(chatGui));
		this.config = config ?? throw new ArgumentNullException(nameof(config));
		this.hintSlot = hintSlot ?? throw new ArgumentNullException(nameof(hintSlot));
		this.log = log;
		chatGui.ChatMessage += OnChatMessage;
	}

	private void OnChatMessage(IHandleableChatMessage message)
	{
		try
		{
			if (!config.Enabled || !config.PreferARankNearHuntHint)
				return;

			var senderText = message.Sender?.TextValue?.Trim();
			var messageText = message.Message?.TextValue;
			MapLinkPayload? mapLink = null;
			if (message.Message != null)
			{
				foreach (var payload in message.Message.Payloads)
				{
					if (payload is MapLinkPayload link)
					{
						mapLink = link;
						break;
					}
				}
			}

			if (!SonarChatHintDecision.ShouldRememberHint(senderText, messageText, mapLink != null))
				return;

			var flag = HuntFlag.FromMapLink(
				mapLink!.TerritoryType.RowId,
				mapLink.Map.RowId,
				mapLink.RawX,
				mapLink.RawY,
				mapLink.PlaceName);
			hintSlot.RememberFromFlag(flag, EngagePositionHintSource.SonarChat);
			log?.Debug(
				$"Sonar soft hint: territory={flag.TerritoryTypeId} raw=({flag.RawX},{flag.RawY})");
		}
		catch (Exception ex)
		{
			log?.Debug($"Sonar chat hint soft-fail: {ex.Message}");
		}
	}

	public void Dispose()
	{
		chatGui.ChatMessage -= OnChatMessage;
	}
}
