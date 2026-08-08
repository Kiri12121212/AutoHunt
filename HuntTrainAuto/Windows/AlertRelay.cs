#nullable enable

using System;
using System.Text;
using HuntTrainAuto.HuntAlerts;

namespace HuntTrainAuto.Windows;

/// <summary>Relay channel list + HuntAlerts-style relay text (pure).</summary>
public static class AlertRelay
{
	public readonly record struct Channel(string Display, string Command);

	public static readonly Channel[] Channels =
	[
		new("Say", "/s"),
		new("Yell", "/y"),
		new("Shout", "/sh"),
		new("Party", "/p"),
		new("Alliance", "/a"),
		new("Free Company", "/fc"),
		new("Linkshell 1", "/l1"),
		new("Linkshell 2", "/l2"),
		new("Linkshell 3", "/l3"),
		new("Linkshell 4", "/l4"),
		new("Linkshell 5", "/l5"),
		new("Linkshell 6", "/l6"),
		new("Linkshell 7", "/l7"),
		new("Linkshell 8", "/l8"),
		new("CWLS 1", "/cwl1"),
		new("CWLS 2", "/cwl2"),
		new("CWLS 3", "/cwl3"),
		new("CWLS 4", "/cwl4"),
		new("CWLS 5", "/cwl5"),
		new("CWLS 6", "/cwl6"),
		new("CWLS 7", "/cwl7"),
		new("CWLS 8", "/cwl8"),
		new("Echo (test)", "/echo"),
	];

	public const string DefaultChannel = "/p";
	public const int MaxChatLineLength = 500;

	public static string DisplayFor(string command)
	{
		foreach (var ch in Channels)
		{
			if (ch.Command.Equals(command, StringComparison.OrdinalIgnoreCase))
				return ch.Display;
		}

		return "Party";
	}

	public static string BuildRelayText(HuntTrainMessage entry)
	{
		ArgumentNullException.ThrowIfNull(entry);

		var isTrain = AlertInfoDisplay.IsTrain(entry);
		var kind = CleanOrFallback(entry.huntKind, "Hunt");
		var world = Clean(entry.huntWorld);
		var creature = Clean(entry.creatureName);
		var zone = Clean(entry.startZone);
		var coords = Clean(entry.locationCoords);
		var ae = Clean(entry.startLocation);
		var inst = entry.instance > 1 ? $" i{entry.instance}" : "";
		var label = isTrain ? "train" : "S Rank";

		var sb = new StringBuilder();
		sb.Append(kind).Append(' ').Append(label);
		if (!isTrain && !string.IsNullOrEmpty(creature))
			sb.Append(' ').Append(creature);
		if (!string.IsNullOrEmpty(world))
			sb.Append(" on ").Append(world);
		sb.Append('!');

		var hasZone = !string.IsNullOrEmpty(zone);
		var hasCoords = !string.IsNullOrEmpty(coords);
		if (hasZone || hasCoords)
		{
			sb.Append(' ');
			if (hasZone)
				sb.Append(zone);
			if (hasZone && hasCoords)
				sb.Append(' ');
			if (hasCoords)
				sb.Append('(').Append(coords).Append(')');
			sb.Append(inst);
		}
		else if (inst.Length > 0)
		{
			sb.Append(inst);
		}

		if (!string.IsNullOrEmpty(ae))
			sb.Append(" - ").Append(ae);
		return sb.ToString();
	}

	public static string BuildChatCommand(HuntTrainMessage entry, string channelCommand)
	{
		var text = BuildRelayText(entry);
		if (string.IsNullOrWhiteSpace(text))
			return string.Empty;

		var channel = string.IsNullOrWhiteSpace(channelCommand) ? DefaultChannel : channelCommand.Trim();
		var line = $"{channel} {text}";
		return line.Length > MaxChatLineLength ? line[..MaxChatLineLength] : line;
	}

	private static string Clean(string? s)
	{
		if (string.IsNullOrWhiteSpace(s))
			return "";
		var t = s.Trim();
		if (t.Equals("invalid", StringComparison.OrdinalIgnoreCase))
			return "";
		if (t.Equals("unknown", StringComparison.OrdinalIgnoreCase))
			return "";
		return t;
	}

	private static string CleanOrFallback(string? s, string fallback)
	{
		var c = Clean(s);
		return string.IsNullOrEmpty(c) ? fallback : c;
	}
}
