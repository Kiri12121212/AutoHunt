#nullable enable

namespace HuntTrainAuto.Combat;

/// <summary>
/// Pure edge-trigger + soft-fail retry for BossMod AI enable (mirrors <see cref="RsrEnableDecision"/>).
/// Observes combat phase only. Latch <c>aiStarted</c> after successful enable until successful disable.
/// </summary>
public static class BossModEnableDecision
{
	public static BossModEnableKind Decide(bool inCombatPhase, bool aiStarted)
	{
		if (inCombatPhase && !aiStarted)
			return BossModEnableKind.StartAi;
		if (!inCombatPhase && aiStarted)
			return BossModEnableKind.Stop;
		return BossModEnableKind.None;
	}

	public static bool NextAiStarted(
		BossModEnableKind kind,
		bool ipcSucceeded,
		bool aiStarted)
	{
		if (!ipcSucceeded)
			return aiStarted;
		return kind switch
		{
			BossModEnableKind.StartAi => true,
			BossModEnableKind.Stop => false,
			_ => aiStarted,
		};
	}

	/// <summary>Abort Clear: stop when we believe AI is on.</summary>
	public static BossModEnableKind DecideClear(bool aiStarted)
		=> aiStarted ? BossModEnableKind.Stop : BossModEnableKind.None;

	/// <summary>Compact, side-effect-free AI decision diagnostic for helper logging.</summary>
	public static string Describe(BossModEnableKind kind)
		=> $"action={kind}";
}
