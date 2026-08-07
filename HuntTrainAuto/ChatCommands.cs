using System;
using System.Collections.Generic;

namespace HuntTrainAuto;

/// <summary>
/// Pure /hta argument handling. Clear uses exact case-insensitive "clear"
/// (after trim), not StartsWith — so "clearfoo" adds a conductor instead of clearing.
/// </summary>
public static class ChatCommands
{
	public static void Handle(
		string arguments,
		IList<string> conductors,
		Action toggleUi,
		Action openUi,
		Action save)
	{
		ArgumentNullException.ThrowIfNull(conductors);
		ArgumentNullException.ThrowIfNull(toggleUi);
		ArgumentNullException.ThrowIfNull(openUi);
		ArgumentNullException.ThrowIfNull(save);

		var args = (arguments ?? string.Empty).Trim();
		if (args.Length == 0)
		{
			toggleUi();
			return;
		}

		if (string.Equals(args, "clear", StringComparison.OrdinalIgnoreCase))
		{
			ConductorList.Clear(conductors);
			save();
			return;
		}

		if (args.StartsWith("add ", StringComparison.OrdinalIgnoreCase))
			args = args[4..].Trim();

		ConductorList.TryAdd(conductors, args);
		save();
		openUi();
	}
}
