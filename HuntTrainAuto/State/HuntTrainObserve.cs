#nullable enable

namespace HuntTrainAuto.State;

/// <summary>
/// Pure signal mapping for the Framework train driver (TASKS 7.2).
/// Converts observable runner / flag state into <see cref="HuntTrainEvent"/> /
/// <see cref="HuntTrainTickSnapshot"/> — no Dalamud types. Soft-fail: never throws.
/// </summary>
public static class HuntTrainObserve
{
	/// <summary>
	/// Idle start event when a hunt flag is adopted.
	/// Teleport plan + mount needed → <see cref="HuntTrainEvent.StartMount"/> (mount before TP);
	/// teleport plan and already mounted / mount off → <see cref="HuntTrainEvent.StartTeleport"/>;
	/// already-close skip → Mount or Navigate by <paramref name="useMount"/>;
	/// otherwise <see cref="HuntTrainEvent.None"/>.
	/// </summary>
	public static HuntTrainEvent DecideFlagStart(
		bool pluginEnabled,
		bool teleportPlanActive,
		bool alreadyCloseSkip,
		bool useMount,
		bool alreadyMountedOrSkipMount = false)
	{
		if (!pluginEnabled)
			return HuntTrainEvent.None;

		if (teleportPlanActive)
		{
			// Mount before casting TP when UseMount and not already travel-ready.
			if (useMount && !alreadyMountedOrSkipMount)
				return HuntTrainEvent.StartMount;
			return HuntTrainEvent.StartTeleport;
		}

		if (!alreadyCloseSkip)
			return HuntTrainEvent.None;

		return useMount ? HuntTrainEvent.StartMount : HuntTrainEvent.StartNavigate;
	}

	/// <summary>
	/// Progress snapshot for <see cref="HuntTrainController.Tick"/>.
	/// Idle start fields stay false except <see cref="HuntTrainTickSnapshot.NeedsTeleport"/>
	/// (Mount→Teleport after mount-before-TP). Flag starts use <see cref="DecideFlagStart"/> + Apply.
	/// </summary>
	/// <param name="pluginEnabled">Master <c>Config.Enabled</c>.</param>
	/// <param name="abort">Hard abort request.</param>
	/// <param name="teleportPlanActive"><see cref="TeleportPlan.HasActive"/>.</param>
	/// <param name="mountJobActive"><see cref="MountRunner.IsActive"/>.</param>
	/// <param name="mounted">Local player <c>ConditionFlag.Mounted</c>.</param>
	/// <param name="inFlight">Local player <c>ConditionFlag.InFlight</c>.</param>
	/// <param name="mountConfig"><see cref="Configuration.Mount"/> (-1 never).</param>
	/// <param name="withinFlagArrival">Flag-area arrival this tick.</param>
	/// <param name="readyForGroundFollow"><see cref="UnmountRunner.ReadyForGroundFollow"/>.</param>
	/// <param name="inCombatPhase"><see cref="CombatSession.InCombatPhase"/>.</param>
	public static HuntTrainTickSnapshot BuildProgressSnapshot(
		bool pluginEnabled,
		bool abort = false,
		bool teleportPlanActive = false,
		bool mountJobActive = false,
		bool mounted = false,
		bool inFlight = false,
		int mountConfig = 0,
		bool withinFlagArrival = false,
		bool readyForGroundFollow = false,
		bool inCombatPhase = false)
		=> new()
		{
			PluginEnabled = pluginEnabled,
			Abort = abort,
			// Mount Decide uses this for Mount→Teleport; Teleport Decide uses TeleportComplete.
			NeedsTeleport = teleportPlanActive,
			TeleportComplete = !teleportPlanActive,
			// Already mounted/in-flight completes Mount even if a WaitReady job is stuck.
			MountComplete = MountDecision.IsTrainMountComplete(
				mountJobActive,
				mounted,
				inFlight,
				mountConfig),
			WithinFlagArrival = withinFlagArrival,
			ReadyForGroundFollow = readyForGroundFollow,
			PartyEngaged = inCombatPhase,
			CombatEnded = !inCombatPhase,
		};
}
