#nullable enable

namespace HuntTrainAuto;

/// <summary>
/// Holds the latest teleport decision / intended <see cref="ArrivalData"/> without executing TP.
/// Filled by chat or later framework wiring; consumers must not treat this as a cast.
/// </summary>
public sealed class TeleportIntent
{
	public TeleportDecisionResult? LatestDecision { get; private set; }

	/// <summary>Arrival from the latest teleporting decision; null when skipped or cleared.</summary>
	public ArrivalData? IntendedArrival { get; private set; }

	public void Set(TeleportDecisionResult decision)
	{
		LatestDecision = decision;
		IntendedArrival = decision.ShouldTeleport ? decision.Arrival : null;
	}

	public void Clear()
	{
		LatestDecision = null;
		IntendedArrival = null;
	}
}

/// <summary>
/// Optional player/local snapshot for decision evaluation (soft-fail when unavailable).
/// Pure data — no Dalamud types.
/// </summary>
public readonly struct TeleportPlayerSnapshot
{
	public required uint CurrentTerritory { get; init; }

	public int CurrentInstance { get; init; }

	/// <summary>
	/// Desired instance for the flag (0 = unspecified). Zone-change callers typically pass
	/// <see cref="TeleportDecision.ResolveZoneChangeInstance"/>.
	/// </summary>
	public int TargetInstance { get; init; }

	/// <summary>Distance to flag in the same units as the config threshold; null if unknown.</summary>
	public float? PlayerDistance { get; init; }

	/// <summary>Nearest aetheryte for the flag territory; null if selection failed.</summary>
	public NearestAetheryteResult? Nearest { get; init; }
}
