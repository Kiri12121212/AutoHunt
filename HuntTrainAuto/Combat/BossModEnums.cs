#nullable enable

namespace HuntTrainAuto.Combat;

/// <summary>Which BossMod family is selected for IPC / chat commands.</summary>
public enum BossModProviderKind
{
	None = 0,

	/// <summary>awgil Boss Mod (<c>InternalName</c> <c>BossMod</c>) — IPC prefix still <c>BossMod</c>.</summary>
	Vbm = 1,

	/// <summary>BossModReborn (<c>InternalName</c> <c>BossModReborn</c>) — same <c>BossMod.*</c> CallGate prefix.</summary>
	Bmr = 2,
}

/// <summary>User preference when both BossMod and BossModReborn are loaded.</summary>
public enum BossModPreference
{
	/// <summary>Prefer BossModReborn when both loaded (Combat Reborn + RSR stack).</summary>
	PreferBmr = 0,

	/// <summary>Prefer awgil Boss Mod when both loaded.</summary>
	PreferVbm = 1,
}

/// <summary>Framework tick outcome for BossMod AI enable (mirrors <see cref="RsrEnableKind"/>).</summary>
public enum BossModEnableKind
{
	None,
	StartAi,
	Stop,
}
