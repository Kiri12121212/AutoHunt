#nullable enable
using System;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using HuntTrainAuto.Contracts;

namespace HuntTrainAuto.Chat;

/// <summary>
/// Minimal game chat / command executor (HTA <c>Chat.ExecuteCommand</c> stand-in).
/// Soft-fails; never throws to callers.
/// </summary>
public sealed unsafe class GameChat : IChatOutput
{
	/// <summary>
	/// Execute a slash command via <c>UIModule.ProcessChatBoxEntry</c>.
	/// Returns false when empty, not a command, or the call fails.
	/// </summary>
	public bool TryExecuteCommand(string command)
	{
		if (string.IsNullOrWhiteSpace(command) || command[0] != '/')
			return false;

		try
		{
			var mes = Utf8String.FromString(command);
			if (mes == null)
				return false;

			UIModule.Instance()->ProcessChatBoxEntry(mes);
			mes->Dtor(true);
			return true;
		}
		catch (Exception)
		{
			return false;
		}
	}
}
