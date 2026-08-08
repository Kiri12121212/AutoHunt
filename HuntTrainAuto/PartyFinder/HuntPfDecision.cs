#nullable enable

namespace HuntTrainAuto.PartyFinder;

/// <summary>Framework tick outcome for hunt Party Finder join.</summary>
public enum HuntPfKind
{
	/// <summary>No agent/UI work this tick.</summary>
	None,

	/// <summary>Request The Hunt category listings (or refresh).</summary>
	RefreshListings,

	/// <summary>Open the chosen listing detail pane.</summary>
	OpenListing,

	/// <summary>Click Join on an open listing detail.</summary>
	ClickJoin,
}

/// <summary>
/// Pure edge + retry gating for auto-join hunt PF after flag arrival.
/// Soft-fail / agent wiring stays in the Framework helper.
/// </summary>
public static class HuntPfDecision
{
	/// <summary>Default wait between refresh / join attempts.</summary>
	public const int DefaultRetryIntervalMs = 3_000;

	public const int MinRetryIntervalMs = 500;
	public const int MaxRetryIntervalMs = 60_000;

	/// <summary>Short settle after OpenListing before attempting Join click.</summary>
	public const int DefaultOpenSettleMs = 750;

	public const int MinOpenSettleMs = 100;
	public const int MaxOpenSettleMs = 5_000;

	public static int ClampRetryIntervalMs(int ms)
	{
		if (ms < MinRetryIntervalMs)
			return MinRetryIntervalMs;
		if (ms > MaxRetryIntervalMs)
			return MaxRetryIntervalMs;
		return ms;
	}

	public static int ClampOpenSettleMs(int ms)
	{
		if (ms < MinOpenSettleMs)
			return MinOpenSettleMs;
		if (ms > MaxOpenSettleMs)
			return MaxOpenSettleMs;
		return ms;
	}

	/// <summary>True when the retry / action throttle has elapsed.</summary>
	public static bool IsActionReady(long nowMs, long nextActionMs)
		=> nowMs >= nextActionMs;

	/// <summary>Schedule the next attempt after <paramref name="intervalMs"/>.</summary>
	public static long NextActionAt(long nowMs, int intervalMs)
		=> nowMs + ClampRetryIntervalMs(intervalMs);

	/// <summary>Schedule join-click settle after opening a listing.</summary>
	public static long NextOpenSettleAt(long nowMs, int settleMs)
		=> nowMs + ClampOpenSettleMs(settleMs);

	/// <summary>
	/// Gate from arrival + party latch + listing/detail readiness.
	/// Skips while in combat so PF UI is not opened mid-fight.
	/// </summary>
	/// <param name="enabled">Config auto-join toggle.</param>
	/// <param name="atHuntStart">Player ready at flag (e.g. ReadyForGroundFollow).</param>
	/// <param name="inCombat">Local player in combat / combat phase — do not thrash PF.</param>
	/// <param name="inParty">Already in a multi-member / CW party.</param>
	/// <param name="joinedLatch">True after we observed a successful join this flag leg.</param>
	/// <param name="hasSuitableListing">Cached hunt PF with open slots.</param>
	/// <param name="detailReadyToJoin">LookingForGroupDetail open with Join enabled.</param>
	/// <param name="pluginOpenedListing">
	/// True after this helper successfully called OpenListing for the current attempt
	/// (do not join a detail pane the player opened manually).
	/// </param>
	/// <param name="actionReady">Throttle elapsed (<see cref="IsActionReady"/>).</param>
	public static HuntPfKind Decide(
		bool enabled,
		bool atHuntStart,
		bool inCombat,
		bool inParty,
		bool joinedLatch,
		bool hasSuitableListing,
		bool detailReadyToJoin,
		bool pluginOpenedListing,
		bool actionReady)
	{
		if (!enabled || !atHuntStart || inCombat)
			return HuntPfKind.None;
		if (joinedLatch || inParty)
			return HuntPfKind.None;
		if (!actionReady)
			return HuntPfKind.None;

		// Join only after our OpenListing — never click an unrelated detail pane.
		if (detailReadyToJoin && pluginOpenedListing && hasSuitableListing)
			return HuntPfKind.ClickJoin;
		if (hasSuitableListing)
			return HuntPfKind.OpenListing;
		return HuntPfKind.RefreshListings;
	}

	/// <summary>
	/// Backward-compatible overload (assumes not in combat).
	/// </summary>
	public static HuntPfKind Decide(
		bool enabled,
		bool atHuntStart,
		bool inParty,
		bool joinedLatch,
		bool hasSuitableListing,
		bool detailReadyToJoin,
		bool pluginOpenedListing,
		bool actionReady)
		=> Decide(
			enabled,
			atHuntStart,
			inCombat: false,
			inParty,
			joinedLatch,
			hasSuitableListing,
			detailReadyToJoin,
			pluginOpenedListing,
			actionReady);

	/// <summary>
	/// Next success latch. Sets when we see <paramref name="inParty"/> while seeking;
	/// clears only via helper Clear (new flag / territory / master off).
	/// </summary>
	public static bool NextJoinedLatch(bool inParty, bool joinedLatch)
	{
		if (joinedLatch)
			return true;
		return inParty;
	}
}
