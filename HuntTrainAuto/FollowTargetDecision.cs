#nullable enable
using System;
using System.Collections.Generic;

namespace HuntTrainAuto;

/// <summary>Which priority tier produced a follow target.</summary>
public enum FollowTargetKind
{
	/// <summary>No usable target — soft-fail (clear/disable follow).</summary>
	None,

	/// <summary>Matched <see cref="Configuration.Conductors"/> (when preferred).</summary>
	Conductor,

	/// <summary>Party leader object.</summary>
	PartyLeader,

	/// <summary>Nearest in-combat party ally.</summary>
	InCombatAlly,
}

/// <summary>
/// Snapshot of one followable actor for pure resolution.
/// Wiring supplies names / flags / distance; no Dalamud types here.
/// </summary>
public readonly struct FollowTargetCandidate
{
	/// <summary>Caller index into the candidate list (returned on pick).</summary>
	public required int Index { get; init; }

	/// <summary>Display / match name (already normalized or raw — matching is caller's job).</summary>
	public required string Name { get; init; }

	/// <summary>True when name matches a configured conductor.</summary>
	public bool IsConductor { get; init; }

	/// <summary>True when this actor is the party leader.</summary>
	public bool IsLeader { get; init; }

	/// <summary>True when the actor is in combat.</summary>
	public bool InCombat { get; init; }

	/// <summary>Distance from local player (yalms). Used to break ties (nearest wins).</summary>
	public float Distance { get; init; }

	/// <summary>Local player — never a follow target.</summary>
	public bool IsLocalPlayer { get; init; }
}

/// <summary>Result of <see cref="FollowTargetDecision.Resolve"/>.</summary>
public readonly struct FollowTargetPick
{
	public required FollowTargetKind Kind { get; init; }

	/// <summary>Index into the candidate list; −1 when <see cref="FollowTargetKind.None"/>.</summary>
	public int Index { get; init; }

	/// <summary>True when a candidate index was selected.</summary>
	public bool Found => Index >= 0;
}

/// <summary>
/// Pure follow-target priority (TASKS 5.2–5.5):
/// conductor (optional) → party leader → nearest in-combat ally.
/// When multiple conductors or in-combat allies match, nearest wins
/// (stable, position-aware; avoids arbitrary object-table order).
/// </summary>
public static class FollowTargetDecision
{
	/// <summary>
	/// Pick a follow target from candidate snapshots.
	/// Soft-fails to <see cref="FollowTargetKind.None"/> when nothing usable is present.
	/// </summary>
	public static FollowTargetPick Resolve(
		IReadOnlyList<FollowTargetCandidate> candidates,
		bool followConductorFirst)
	{
		ArgumentNullException.ThrowIfNull(candidates);

		if (followConductorFirst)
		{
			var conductor = PickNearest(candidates, static c => c.IsConductor);
			if (conductor.Found)
				return new FollowTargetPick { Kind = FollowTargetKind.Conductor, Index = conductor.Index };
		}

		var leader = PickFirst(candidates, static c => c.IsLeader);
		if (leader.Found)
			return new FollowTargetPick { Kind = FollowTargetKind.PartyLeader, Index = leader.Index };

		var ally = PickNearest(candidates, static c => c.InCombat);
		if (ally.Found)
			return new FollowTargetPick { Kind = FollowTargetKind.InCombatAlly, Index = ally.Index };

		return None();
	}

	/// <summary>Soft-fail: clear / leave follow disabled.</summary>
	public static FollowTargetPick None()
		=> new() { Kind = FollowTargetKind.None, Index = -1 };

	private static FollowTargetPick PickFirst(
		IReadOnlyList<FollowTargetCandidate> candidates,
		Func<FollowTargetCandidate, bool> predicate)
	{
		for (var i = 0; i < candidates.Count; i++)
		{
			var c = candidates[i];
			if (c.IsLocalPlayer || !predicate(c))
				continue;

			return new FollowTargetPick { Kind = FollowTargetKind.None, Index = c.Index };
		}

		return None();
	}

	private static FollowTargetPick PickNearest(
		IReadOnlyList<FollowTargetCandidate> candidates,
		Func<FollowTargetCandidate, bool> predicate)
	{
		var bestIndex = -1;
		var bestDistance = float.PositiveInfinity;

		for (var i = 0; i < candidates.Count; i++)
		{
			var c = candidates[i];
			if (c.IsLocalPlayer || !predicate(c))
				continue;

			if (c.Distance < bestDistance)
			{
				bestDistance = c.Distance;
				bestIndex = c.Index;
			}
		}

		if (bestIndex < 0)
			return None();

		return new FollowTargetPick { Kind = FollowTargetKind.None, Index = bestIndex };
	}
}
