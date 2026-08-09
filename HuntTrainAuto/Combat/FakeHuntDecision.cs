#nullable enable
using System;
using System.Numerics;

namespace HuntTrainAuto.Combat;

/// <summary>Which Debug-tab Fake Hunt preset was requested.</summary>
public enum FakeHuntPreset
{
	Near,
	Far,
	MapFlag,
	/// <summary>Far flag + mismatched <c>ReportedInstance</c> → same-zone SwitchInstance.</summary>
	InstanceSwap,
}

/// <summary>
/// Pure Fake Hunt helpers (offsets, raw coords, auto-end). No Dalamud.
/// </summary>
public static class FakeHuntDecision
{
	/// <summary>
	/// Same-zone mid-range flag (former Far): beyond default AlreadyClose (150) so walk/TP decides.
	/// </summary>
	public const float NearFlagDistanceYalms = 250f;

	/// <summary>
	/// Across most of a typical hunt zone so AutoTeleport / aetheryte TP is forced.
	/// Also used by <see cref="FakeHuntPreset.InstanceSwap"/> so AlreadyClose does not beat SwitchInstance.
	/// </summary>
	public const float FarFlagDistanceYalms = 1000f;

	public const float FakeARankMinDistanceYalms = 20f;
	public const float FakeARankMaxDistanceYalms = 40f;

	/// <summary>Auto-clear combat after fake EnterCombat so remount can be verified.</summary>
	public const int DefaultAutoEndCombatMs = 5_000;

	/// <summary>World XZ offset from <paramref name="origin"/> (Y preserved).</summary>
	public static Vector3 OffsetWorldXZ(Vector3 origin, float distanceYalms, float angleRad)
	{
		if (float.IsNaN(distanceYalms) || float.IsInfinity(distanceYalms) || distanceYalms < 0f)
			distanceYalms = 0f;
		if (float.IsNaN(angleRad) || float.IsInfinity(angleRad))
			angleRad = 0f;

		var dx = MathF.Cos(angleRad) * distanceYalms;
		var dz = MathF.Sin(angleRad) * distanceYalms;
		return new Vector3(origin.X + dx, origin.Y, origin.Z + dz);
	}

	/// <summary>Inverse of <see cref="Map.FlagWorldPosition.WorldXZFromRaw"/>.</summary>
	public static (int RawX, int RawY) RawFromWorldXZ(float worldX, float worldZ)
		=> ((int)(worldX * 1000f), (int)(worldZ * 1000f));

	public static float FlagDistanceForPreset(FakeHuntPreset preset)
		=> preset switch
		{
			FakeHuntPreset.Far => FarFlagDistanceYalms,
			FakeHuntPreset.InstanceSwap => FarFlagDistanceYalms,
			FakeHuntPreset.Near => NearFlagDistanceYalms,
			_ => NearFlagDistanceYalms,
		};

	/// <summary>Compact, side-effect-free preset diagnostic for helper logging.</summary>
	public static string Describe(FakeHuntPreset preset)
		=> $"preset={preset}, flagDistance={FlagDistanceForPreset(preset):0.0}";

	/// <summary>
	/// Conductor-style alternate instance for Fake Hunt swap (1↔2).
	/// <paramref name="currentInstance"/> ≤0 treated as 1 (unsplit coerce).
	/// </summary>
	public static int AlternateReportedInstance(int currentInstance)
	{
		var cur = currentInstance > 0 ? currentInstance : 1;
		return cur <= 1 ? 2 : 1;
	}

	/// <summary>
	/// Deterministic-ish angle from tick; distance in [min,max] from unit fraction.
	/// </summary>
	public static float FakeARankDistanceYalms(float unit01)
	{
		var t = unit01;
		if (float.IsNaN(t) || float.IsInfinity(t))
			t = 0f;
		if (t < 0f)
			t = 0f;
		if (t > 1f)
			t = 1f;
		return FakeARankMinDistanceYalms
			+ t * (FakeARankMaxDistanceYalms - FakeARankMinDistanceYalms);
	}

	public static float AngleFromSeed(int seed)
	{
		// Full circle from non-negative seed bits.
		var u = (uint)seed;
		return (u % 360) * (MathF.PI / 180f);
	}

	public static bool ShouldAutoEndCombat(long enteredCombatAtMs, long nowMs, int autoEndMs)
	{
		if (enteredCombatAtMs <= 0)
			return false;
		var wait = autoEndMs > 0 ? autoEndMs : DefaultAutoEndCombatMs;
		return nowMs - enteredCombatAtMs >= wait;
	}

	/// <summary>
	/// Synthetic A has no object-table entity — RSR/BossMod would spam failed UseAction
	/// ("Cannot execute … at this time"). Path/divert/engage still use <paramref name="inCombatPhase"/>.
	/// </summary>
	public static bool ShouldEnableCombatAi(bool inCombatPhase, bool fakeHuntActive)
		=> inCombatPhase && !fakeHuntActive;

	public static string PlaceNameForPreset(FakeHuntPreset preset)
		=> preset switch
		{
			FakeHuntPreset.Far => "FakeHunt Far",
			FakeHuntPreset.MapFlag => "FakeHunt MapFlag",
			FakeHuntPreset.InstanceSwap => "FakeHunt InstanceSwap",
			_ => "FakeHunt Near",
		};
}
