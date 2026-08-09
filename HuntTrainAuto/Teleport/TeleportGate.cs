#nullable enable

namespace HuntTrainAuto.Teleport;

/// <summary>
/// Pure gate conditions for Framework teleport attempts (HTA <c>Framework_Update</c> core).
/// Condition / player wiring stays in plugin code.
/// </summary>
public static class TeleportGate
{
	/// <summary>Compact, side-effect-free gate diagnostic for call-site logging.</summary>
	public static string DescribeBlock(
		bool inCombat,
		bool betweenAreas,
		bool betweenAreas51,
		bool casting,
		bool isMoving,
		bool animationLocked,
		bool teleportInvoked)
		=> $"blocked: invoked={teleportInvoked}, combat={inCombat}, betweenAreas={betweenAreas || betweenAreas51}, "
			+ $"casting={casting}, moving={isMoving}, animationLocked={animationLocked}";

	/// <summary>
	/// Soft screen-ready check (HTA <c>IsScreenReady</c> subset without ECommons).
	/// </summary>
	public static bool IsScreenReady(
		bool betweenAreas,
		bool betweenAreas51,
		bool occupiedInCutScene,
		bool watchingCutscene)
		=> !betweenAreas
			&& !betweenAreas51
			&& !occupiedInCutScene
			&& !watchingCutscene;

	/// <summary>
	/// Idle grace after cast ends (or invoke never cast) before releasing
	/// <c>TeleportInvoked</c> for a retry. Must outlast normal cast→BetweenAreas
	/// so a successful TP is not re-fired mid-transition.
	/// </summary>
	public const int PostInvokeIdleRetryMs = 6000;

	/// <summary>
	/// Whether the Framework loop may call Teleporter/Lifestream this tick
	/// (HTA: <c>!InCombat &amp;&amp; !BetweenAreas* &amp;&amp; !Casting &amp;&amp; !IsMoving</c>,
	/// plus animation lock; blocked while an invoke is already in flight).
	/// </summary>
	public static bool CanAttemptTeleport(
		bool inCombat,
		bool betweenAreas,
		bool betweenAreas51,
		bool casting,
		bool isMoving,
		bool animationLocked = false,
		bool teleportInvoked = false)
		=> !teleportInvoked
			&& !inCombat
			&& !betweenAreas
			&& !betweenAreas51
			&& !casting
			&& !isMoving
			&& !animationLocked;

	/// <summary>
	/// Release <c>TeleportInvoked</c> so a cancelled / stuck TP can retry.
	/// Requires continuous idle (not casting) for <paramref name="idleRetryMs"/>
	/// without BetweenAreas — successful hops clear via plan handoff sooner.
	/// <paramref name="idleSinceMs"/> ≤ 0 means not idle yet (still casting / just invoked).
	/// </summary>
	public static bool ShouldReleaseTeleportInvoked(
		bool teleportInvoked,
		bool casting,
		bool isCasting,
		bool betweenAreas,
		bool betweenAreas51,
		long idleSinceMs,
		long nowMs,
		int idleRetryMs = PostInvokeIdleRetryMs)
	{
		if (!teleportInvoked || IsBetweenAreas(betweenAreas, betweenAreas51))
			return false;
		if (casting || isCasting || idleSinceMs <= 0)
			return false;
		return nowMs - idleSinceMs >= idleRetryMs;
	}

	/// <summary>
	/// Player is usable for teleport attempts (vanilla stand-in for ECommons <c>Player.Interactable</c>).
	/// </summary>
	public static bool IsPlayerReady(bool hasLocalPlayer, bool hpAboveZero, bool unconscious)
		=> hasLocalPlayer && hpAboveZero && !unconscious;

	/// <summary>
	/// Plugin/config allow auto-teleport execution.
	/// </summary>
	public static bool IsAutoTeleportEnabled(bool pluginEnabled, bool autoTeleport)
		=> pluginEnabled && autoTeleport;

	/// <summary>
	/// Between-areas transition that should clear the active plan and run post-TP hooks.
	/// </summary>
	public static bool IsBetweenAreas(bool betweenAreas, bool betweenAreas51)
		=> betweenAreas || betweenAreas51;

	/// <summary>
	/// BetweenAreas handoff: clear plan + mount only after we actually invoked Teleporter/Lifestream.
	/// Prevents residual / unrelated BetweenAreas from wiping a plan that never fired.
	/// </summary>
	public static bool ShouldClearPlanOnBetweenAreas(
		bool betweenAreas,
		bool betweenAreas51,
		bool hasActivePlan,
		bool teleportInvoked)
		=> hasActivePlan
			&& teleportInvoked
			&& IsBetweenAreas(betweenAreas, betweenAreas51);

	/// <summary>
	/// Whether to enqueue instance change after teleport (see <see cref="InstanceChangeDecision.ShouldEnqueue"/>).
	/// </summary>
	public static bool ShouldEnqueueInstanceChange(int instance)
		=> InstanceChangeDecision.ShouldEnqueue(instance);

}
