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
	/// Last applied edge, suitable for side-effecting callers to log. Null when no phase changed.
	/// </summary>
	public string? LastTransitionDescription { get; private set; }

	/// <summary>
	/// Apply a discrete event. Illegal / <see cref="HuntTrainEvent.None"/> → no-op (stay).
	/// Returns the phase after the attempt.
	/// </summary>
	public HuntTrainPhase Apply(HuntTrainEvent ev)
	{
		var from = Phase;
		Phase = HuntTrainTransition.Apply(from, ev);
		LastTransitionDescription = from != Phase
			? HuntTrainTransition.Describe(from, ev, Phase)
			: null;
		return Phase;
	}

	/// <summary>
	/// Soft tick: decide one event from <paramref name="snap"/> then apply.
	/// Null-safe by value — default snapshot yields stay / Idle abort no-op.
	/// </summary>
	public HuntTrainPhase Tick(in HuntTrainTickSnapshot snap)
	{
		var from = Phase;
		var ev = HuntTrainTransition.Decide(from, snap);
		Phase = HuntTrainTransition.Apply(from, ev);
		LastTransitionDescription = from != Phase
			? HuntTrainTransition.Describe(from, ev, Phase)
			: null;
		return Phase;
	}

	/// <summary>Force Idle (master off, territory hard clear, dispose).</summary>
	public void Reset()
	{
		var from = Phase;
		Phase = HuntTrainPhase.Idle;
		LastTransitionDescription = from != Phase
			? $"phase reset: {from} -> {Phase}"
			: null;
	}

	/// <summary>Alias for <see cref="Reset"/> (matches Domain session Clear naming).</summary>
	public void Clear() => Reset();
}
