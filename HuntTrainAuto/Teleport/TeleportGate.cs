#nullable enable

namespace HuntTrainAuto.Teleport;

/// <summary>
/// Pure gate conditions for Framework teleport attempts (HTA <c>Framework_Update</c> core).
/// Condition / player wiring stays in plugin code.
/// </summary>
public static class TeleportGate
{
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
	/// Whether the Framework loop may call Teleporter/Lifestream this tick
	/// (HTA: <c>!InCombat &amp;&amp; !BetweenAreas* &amp;&amp; !Casting &amp;&amp; !IsMoving</c>, plus animation lock).
	/// </summary>
	public static bool CanAttemptTeleport(
		bool inCombat,
		bool betweenAreas,
		bool betweenAreas51,
		bool casting,
		bool isMoving,
		bool animationLocked = false)
		=> !inCombat
			&& !betweenAreas
			&& !betweenAreas51
			&& !casting
			&& !isMoving
			&& !animationLocked;

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
