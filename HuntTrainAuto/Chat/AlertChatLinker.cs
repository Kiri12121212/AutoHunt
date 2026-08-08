#nullable enable

using System;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using HuntTrainAuto.HuntAlerts;
using HuntTrainAuto.Windows;

namespace HuntTrainAuto.Chat;

/// <summary>
/// HuntAlerts-style clickable chat lines: ring of <see cref="DalamudLinkPayload"/> handlers
/// that open alert info for the cached <see cref="HuntTrainMessage"/>.
/// </summary>
public sealed class AlertChatLinker : IDisposable
{
	public const int Capacity = 50;

	private readonly IChatGui chatGui;
	private readonly IPluginLog? log;
	private readonly Action<HuntTrainMessage> openAlert;
	private readonly DalamudLinkPayload[] payloads = new DalamudLinkPayload[Capacity];
	private readonly HuntTrainMessage?[] messages = new HuntTrainMessage?[Capacity];
	private int commandCount;
	private bool disposed;

	public AlertChatLinker(
		IChatGui chatGui,
		Action<HuntTrainMessage> openAlert,
		IPluginLog? log = null)
	{
		this.chatGui = chatGui ?? throw new ArgumentNullException(nameof(chatGui));
		this.openAlert = openAlert ?? throw new ArgumentNullException(nameof(openAlert));
		this.log = log;

		for (var i = 0u; i < Capacity; i++)
			payloads[i] = chatGui.AddChatLinkHandler(i, OnLink);
	}

	/// <summary>Cache <paramref name="message"/> and print a clickable HuntAlerts-style notice.</summary>
	public void Post(HuntTrainMessage message)
	{
		if (disposed)
			return;
		ArgumentNullException.ThrowIfNull(message);

		try
		{
			var slot = commandCount % Capacity;
			commandCount++;
			var snapshot = message.Clone();
			messages[slot] = snapshot;
			var link = payloads[slot];
			var text = AlertChatNotice.FormatLine(snapshot);
			var color = AlertChatNotice.UiForegroundFor(snapshot);
			chatGui.Print(BuildLinkedLine(link, text, color));
		}
		catch (Exception ex)
		{
			log?.Debug($"AlertChatLinker.Post soft-fail: {ex.Message}");
		}
	}

	private void OnLink(uint cmd, SeString _)
	{
		try
		{
			if (cmd >= Capacity)
				return;
			var msg = messages[cmd];
			if (msg == null)
				return;
			openAlert(msg);
		}
		catch (Exception ex)
		{
			log?.Debug($"AlertChatLinker.OnLink soft-fail: {ex.Message}");
		}
	}

	internal static SeString BuildLinkedLine(DalamudLinkPayload link, string text, ushort color)
	{
		var b = new SeStringBuilder();
		if (color != 0)
			b.AddUiForeground(color);
		b.Add(link).AddText(text).Add(RawPayload.LinkTerminator);
		if (color != 0)
			b.AddUiForegroundOff();
		return b.Build();
	}

	public void Dispose()
	{
		if (disposed)
			return;
		disposed = true;
		try
		{
			chatGui.RemoveChatLinkHandler();
		}
		catch
		{
			// soft-fail dispose
		}
	}
}
