#nullable enable
using System.Numerics;
using HuntTrainAuto.Combat;

namespace HuntTrainAuto.Domain;

/// <summary>
/// Active Debug Fake Hunt: injected flag + optional synthetic NearbyARank WorldPos.
/// </summary>
public sealed class FakeHuntSession
{
	public bool IsActive => ActiveFlag != null;

	public HuntFlag? ActiveFlag { get; private set; }

	public FakeHuntPreset Preset { get; private set; }

	/// <summary>Synthetic A-rank floor position; null when cleared / after combat end.</summary>
	public Vector3? FakeARankWorldPos { get; private set; }

	/// <summary><see cref="Environment.TickCount64"/> when fake EnterCombat fired; 0 if not in combat.</summary>
	public long EnteredCombatAtMs { get; private set; }

	/// <summary>
	/// True after a successful RSR+BossMod force-stop this Fake Hunt session
	/// (sticky AutoDuty can outlive plugin reload).
	/// </summary>
	public bool CombatAiSuppressed { get; private set; }

	public int AutoEndCombatMs { get; private set; } = FakeHuntDecision.DefaultAutoEndCombatMs;

	public void Arm(
		HuntFlag flag,
		FakeHuntPreset preset,
		Vector3? fakeARankWorldPos,
		int autoEndCombatMs = FakeHuntDecision.DefaultAutoEndCombatMs)
	{
		ActiveFlag = flag;
		Preset = preset;
		FakeARankWorldPos = fakeARankWorldPos is { } p && p != Vector3.Zero ? p : null;
		EnteredCombatAtMs = 0;
		CombatAiSuppressed = false;
		AutoEndCombatMs = autoEndCombatMs > 0
			? autoEndCombatMs
			: FakeHuntDecision.DefaultAutoEndCombatMs;
	}

	public void NoteEnteredCombat(long nowMs)
	{
		if (!IsActive)
			return;
		EnteredCombatAtMs = nowMs > 0 ? nowMs : 1;
	}

	public void NoteCombatAiSuppressed() => CombatAiSuppressed = true;

	/// <summary>Drop fake A so divert/engage stop; keep session Active for Status until Clear.</summary>
	public void ClearFakeARank()
	{
		FakeARankWorldPos = null;
		EnteredCombatAtMs = 0;
	}

	public void Clear()
	{
		ActiveFlag = null;
		Preset = FakeHuntPreset.Near;
		FakeARankWorldPos = null;
		EnteredCombatAtMs = 0;
		CombatAiSuppressed = false;
		AutoEndCombatMs = FakeHuntDecision.DefaultAutoEndCombatMs;
	}
}
