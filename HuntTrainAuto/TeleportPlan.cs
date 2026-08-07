#nullable enable

namespace HuntTrainAuto;

/// <summary>
/// Active Framework teleport target (HTA <c>TeleportTo</c>).
/// Distinct from <see cref="TeleportIntent"/> (decision storage); this is the executable plan.
/// </summary>
public sealed class TeleportPlan
{
	/// <summary>Current arrival to chase; null when idle.</summary>
	public ArrivalData? Active { get; private set; }

	public bool HasActive => Active != null;

	/// <summary>Adopt an arrival as the active Framework plan.</summary>
	public void Set(ArrivalData arrival)
	{
		Active = arrival;
	}

	/// <summary>
	/// When intent says teleport and has arrival, adopt it and return true.
	/// When intent should not teleport (skip / no arrival), clears any active plan and returns false.
	/// Does not clear <paramref name="intent"/>.
	/// </summary>
	public bool TryAdoptFromIntent(TeleportIntent intent)
	{
		var arrival = intent.IntendedArrival;
		if (arrival == null)
		{
			Active = null;
			return false;
		}

		Active = arrival;
		return true;
	}

	public void Clear()
	{
		Active = null;
	}
}
