#nullable enable

namespace HuntTrainAuto.Domain;

/// <summary>
/// Active Framework teleport target (HTA <c>TeleportTo</c>).
/// Distinct from <see cref="TeleportIntent"/> (decision storage); this is the executable plan.
/// </summary>
public sealed class TeleportPlan
{
	/// <summary>Current arrival to chase; null when idle.</summary>
	public ArrivalData? Active { get; private set; }

	public bool HasActive => Active != null;

	/// <summary>
	/// True after Teleporter/Lifestream accepted an invoke for this plan.
	/// BetweenAreas handoff must not clear the plan until this is set — otherwise a
	/// residual or unrelated BetweenAreas flash aborts TP and falls through to long fly-to.
	/// </summary>
	public bool TeleportInvoked { get; private set; }

	/// <summary>Adopt an arrival as the active Framework plan.</summary>
	public void Set(ArrivalData arrival)
	{
		Active = arrival;
		TeleportInvoked = false;
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
			TeleportInvoked = false;
			return false;
		}

		Active = arrival;
		TeleportInvoked = false;
		return true;
	}

	/// <summary>Record that Teleporter/Lifestream accepted an invoke for the active plan.</summary>
	public void MarkTeleportInvoked()
	{
		if (Active != null)
			TeleportInvoked = true;
	}

	/// <summary>
	/// Drop the invoke latch without clearing the plan (cancelled cast / stuck retry).
	/// </summary>
	public void ClearTeleportInvoked()
	{
		TeleportInvoked = false;
	}

	public void Clear()
	{
		Active = null;
		TeleportInvoked = false;
	}
}
