#nullable enable

namespace HuntTrainAuto.Domain;

/// <summary>
/// <see cref="Lumina.Excel.Sheets.NotoriousMonster.Rank"/> byte values used by the client.
/// Verified against MobHunt / community hunt tools: B=1, A=2, S=3.
/// </summary>
public enum HuntMarkRank : byte
{
	None = 0,
	B = 1,
	A = 2,
	S = 3,
}
