using System;
using System.Collections.Generic;

namespace HuntTrainAuto.Chat;

public static class ConductorList
{
	public static bool TryAdd(IList<string> conductors, string name)
	{
		ArgumentNullException.ThrowIfNull(conductors);

		var trimmed = name?.Trim();
		if (string.IsNullOrEmpty(trimmed))
			return false;

		for (var i = 0; i < conductors.Count; i++)
		{
			if (string.Equals(conductors[i], trimmed, StringComparison.OrdinalIgnoreCase))
				return false;
		}

		conductors.Add(trimmed);
		return true;
	}

	public static bool TryRemoveAt(IList<string> conductors, int index)
	{
		ArgumentNullException.ThrowIfNull(conductors);

		if (index < 0 || index >= conductors.Count)
			return false;

		conductors.RemoveAt(index);
		return true;
	}

	public static void Clear(IList<string> conductors)
	{
		ArgumentNullException.ThrowIfNull(conductors);
		conductors.Clear();
	}
}
