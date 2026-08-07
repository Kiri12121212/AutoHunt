#nullable enable

namespace HuntTrainAuto.State;

/// <summary>
/// Mutable train pipeline state machine (TASKS 7.1).
/// Framework driver feeds signals via <see cref="HuntTrainObserve"/> (7.2) — this type holds phase + Apply/Tick/Reset.
/// </summary>
public sealed class HuntTrainController
{
	public HuntTrainPhase Phase { get; private set; } = HuntTrainPhase.Idle;

	/// <summary>True while any non-Idle pipeline phase is active.</summary>
	public bool IsActive => Phase != HuntTrainPhase.Idle;

	/// <summary>
	/// Apply a discrete event. Illegal / <see cref="HuntTrainEvent.None"/> → no-op (stay).
	/// Returns the phase after the attempt.
	/// </summary>
	public HuntTrainPhase Apply(HuntTrainEvent ev)
	{
		Phase = HuntTrainTransition.Apply(Phase, ev);
		return Phase;
	}

	/// <summary>
	/// Soft tick: decide one event from <paramref name="snap"/> then apply.
	/// Null-safe by value — default snapshot yields stay / Idle abort no-op.
	/// </summary>
	public HuntTrainPhase Tick(in HuntTrainTickSnapshot snap)
	{
		Phase = HuntTrainTransition.Tick(Phase, snap);
		return Phase;
	}

	/// <summary>Force Idle (master off, territory hard clear, dispose).</summary>
	public void Reset() => Phase = HuntTrainPhase.Idle;

	/// <summary>Alias for <see cref="Reset"/> (matches Domain session Clear naming).</summary>
	public void Clear() => Reset();
}
