#nullable enable
using System.Collections.Generic;

namespace HuntTrainAuto.Map;

/// <summary>
/// Pure A-rank NameId / BNpcBase index built from NotoriousMonster rows.
/// Game wiring loads the sheet; tests pass synthetic (id, rank) pairs.
/// </summary>
public static class ARankHuntIndex
{
	/// <summary>
	/// Collect BNpcName and BNpcBase row ids for <see cref="HuntMarkRank.A"/> only.
	/// S/B/other ranks are ignored.
	/// </summary>
	public static HashSet<uint> BuildARankIds(
		IEnumerable<(uint BNpcNameId, uint BNpcBaseId, byte Rank)> rows)
	{
		var ids = new HashSet<uint>();
		foreach (var (nameId, baseId, rank) in rows)
		{
			if (rank != (byte)HuntMarkRank.A)
				continue;
			if (nameId != 0)
				ids.Add(nameId);
			if (baseId != 0)
				ids.Add(baseId);
		}

		return ids;
	}

	/// <summary>True when <paramref name="nameId"/> or <paramref name="baseId"/> is a known A-rank.</summary>
	public static bool IsARank(IReadOnlySet<uint> aRankIds, uint nameId, uint baseId)
		=> (nameId != 0 && aRankIds.Contains(nameId))
			|| (baseId != 0 && aRankIds.Contains(baseId));
}
