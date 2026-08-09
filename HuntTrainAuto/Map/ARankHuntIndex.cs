#nullable enable
using System.Collections.Generic;
using HuntTrainAuto.Domain;

namespace HuntTrainAuto.Map;

/// <summary>
/// A-rank NameId / BNpcBase index built from NotoriousMonster rows.
/// Name and base ids live in <b>separate</b> namespaces — never merge into one set
/// (trash <c>BaseId</c> can equal an A-rank <c>BnpcName</c> id).
/// </summary>
public readonly struct ARankIdIndex
{
	public required HashSet<uint> NameIds { get; init; }

	public required HashSet<uint> BaseIds { get; init; }

	public int Count => NameIds.Count + BaseIds.Count;

	public static ARankIdIndex Empty { get; } = new()
	{
		NameIds = [],
		BaseIds = [],
	};
}

/// <summary>
/// Pure A-rank NameId / BNpcBase index built from NotoriousMonster rows.
/// Game wiring loads the sheet; tests pass synthetic (id, rank) pairs.
/// </summary>
public static class ARankHuntIndex
{
	/// <summary>
	/// Collect BNpcName and BNpcBase row ids for <see cref="HuntMarkRank.A"/> only.
	/// S/B/other ranks are ignored. Ids stay in separate sets.
	/// </summary>
	public static ARankIdIndex BuildARankIds(
		IEnumerable<(uint BnpcNameId, uint BnpcBaseId, byte Rank)> rows)
	{
		var nameIds = new HashSet<uint>();
		var baseIds = new HashSet<uint>();
		foreach (var (nameId, baseId, rank) in rows)
		{
			if (rank != (byte)HuntMarkRank.A)
				continue;
			if (nameId != 0)
				nameIds.Add(nameId);
			if (baseId != 0)
				baseIds.Add(baseId);
		}

		return new ARankIdIndex { NameIds = nameIds, BaseIds = baseIds };
	}

	/// <summary>
	/// True when <paramref name="nameId"/> is a known A-rank name <b>or</b>
	/// <paramref name="baseId"/> is a known A-rank base (never cross-namespace).
	/// </summary>
	public static bool IsARank(in ARankIdIndex index, uint nameId, uint baseId)
		=> (nameId != 0 && index.NameIds.Contains(nameId))
			|| (baseId != 0 && index.BaseIds.Contains(baseId));
}
