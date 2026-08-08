using System;
using System.Collections.Generic;
using Dalamud.Configuration;

namespace HuntTrainAuto;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
	/// <summary>Bump when persisted fields need one-time migration (see Plugin ctor).</summary>
	public int Version { get; set; } = 2;

	public bool Enabled { get; set; } = true;
	public List<string> Conductors { get; set; } = [];
	public bool SuppressChatOtherPlayers { get; set; } = true;
	/// <summary>
	/// Native <c>IContextMenu</c> + ChatTwo IPC: "Add as conductor" / "[HTA] Set as conductor".
	/// </summary>
	public bool ContextMenu { get; set; } = true;
	public bool AutoOpenMap { get; set; } = true;
	public bool NoDuplicateFlags { get; set; } = true;

	/// <summary>HTA parity: auto-teleport on conductor flags (zone / instance / far).</summary>
	public bool AutoTeleport { get; set; } = true;

	/// <summary>
	/// Same-zone skip threshold in yalms: if player↔flag world XZ ≤ this, skip aetheryte TP
	/// (mount/nav later). Default 150 ≈ former HTA 3 map-units on sizeFactor 100 maps.
	/// Config Version &lt; 2 stored map units; Plugin migrates on load (×50, clamp).
	/// Soft-fallback when time-aware path costs are unavailable; optional floor when time-aware is on.
	/// </summary>
	public float AutoTeleportAetheryteDistanceDiff { get; set; } = 150f;

	/// <summary>
	/// Same-zone: compare vnav path ride times vs TP overhead instead of (only) flat distance.
	/// Soft-fails to <see cref="AutoTeleportAetheryteDistanceDiff"/> when vnav/pathfind fails.
	/// </summary>
	public bool AutoTeleportTimeAware { get; set; } = true;

	/// <summary>Teleport cast duration estimate (seconds; Action 5 ≈ 5s).</summary>
	public float AutoTeleportCastSeconds { get; set; } = SameZoneTravelCost.DefaultCastSeconds;

	/// <summary>Configurable BetweenAreas load estimate (seconds).</summary>
	public float AutoTeleportLoadEstimateSeconds { get; set; } = SameZoneTravelCost.DefaultLoadEstimateSeconds;

	/// <summary>Assumed mount speed for ride-time estimates (yalms / second).</summary>
	public float AutoTeleportMountSpeedYalmsPerSec { get; set; } = SameZoneTravelCost.DefaultMountSpeedYalmsPerSec;

	/// <summary>Optional mount-up overhead added to both ride legs (seconds).</summary>
	public float AutoTeleportMountUpSeconds { get; set; } = SameZoneTravelCost.DefaultMountUpSeconds;

	/// <summary>
	/// When time-aware is on, still skip TP if distance ≤ <see cref="AutoTeleportAetheryteDistanceDiff"/>.
	/// </summary>
	public bool AutoTeleportRetainDistanceFloor { get; set; } = true;

	/// <summary>
	/// When teleporting to another zone, set arrival instance to 1 (HTA parity).
	/// Used when building target instance for the decision API — does not call Lifestream.
	/// </summary>
	public bool AutoSwitchInstanceToOne { get; set; } = false;

	/// <summary>HTA <c>TeleportDelayEnabled</c>: random pre-delay before first TP attempt.</summary>
	public bool TeleportDelayEnabled { get; set; } = false;

	/// <summary>HTA <c>TeleportDelayMin</c> (ms).</summary>
	public int TeleportDelayMin { get; set; } = 200;

	/// <summary>HTA <c>TeleportDelayMax</c> (ms).</summary>
	public int TeleportDelayMax { get; set; } = 700;

	/// <summary>Aetheryte RowIds excluded from nearest-aetheryte selection (HTA default included 148; we start empty).</summary>
	public List<uint> AetheryteBlacklist { get; set; } = [];

	/// <summary>HTA distance-compensation hack for named aetherytes. Default false (HTA parity).</summary>
	public bool DistanceCompensationHack { get; set; } = false;

	/// <summary>HTA <c>UseMount</c>: enqueue auto-mount after TP / instance settle.</summary>
	public bool UseMount { get; set; } = true;

	/// <summary>
	/// HTA <c>Mount</c> RowId: <c>0</c> = random (GeneralAction 9), <c>-1</c> = never mount,
	/// other = specific mount (falls back if locked).
	/// </summary>
	public int Mount { get; set; } = 0;

	/// <summary>
	/// Hunt-flag area arrival radius (yalms). When player distance to
	/// <see cref="HuntFlag.WorldPos"/> ≤ this, stop vnavmesh path (ready for unmount).
	/// Larger than AD path tolerance (0.25) — flags mark an area, not a point.
	/// </summary>
	public float FlagArrivalTolerance { get; set; } = FlagArrival.DefaultTolerance;

	/// <summary>
	/// Auto-dismount at flag area after arrival (TaskUnmount / GeneralAction 1 or <c>/dismount</c>).
	/// Default true. After success, subsequent follow nav should use <c>canFly: false</c>.
	/// </summary>
	public bool AutoUnmountAtFlag { get; set; } = true;

	/// <summary>
	/// After flag arrival / unmount, find a The Hunt Party Finder listing and join it.
	/// Retries until joined (or disabled / new flag). Soft-fails agent/UI join attempts.
	/// Default off — opt-in until the join path is trusted in live trains.
	/// </summary>
	public bool AutoJoinHuntPf { get; set; } = false;

	/// <summary>
	/// Milliseconds between hunt PF refresh / join retries.
	/// Clamped via <see cref="HuntPfDecision.ClampRetryIntervalMs"/>.
	/// </summary>
	public int HuntPfRetryIntervalMs { get; set; } = HuntPfDecision.DefaultRetryIntervalMs;

	/// <summary>
	/// Party-stack follow distance (yalms). Default <see cref="FollowDecision.DefaultFollowDistance"/>.
	/// Effective value is clamped to
	/// [<see cref="FollowDecision.MinFollowDistance"/>, <see cref="FollowDecision.MaxFollowDistance"/>].
	/// </summary>
	public float PartyFollowDistance { get; set; } = FollowDecision.DefaultFollowDistance;
	public bool FollowConductorFirst { get; set; } = true;

	/// <summary>
	/// Scan radius (yalms) for nearby A-rank NotoriousMonsters after unmount.
	/// Conductor-fight join ignores this and uses the conductor's target.
	/// </summary>
	public float ARankScanRange { get; set; } = EngageTargetDecision.DefaultARankScanRange;

	/// <summary>
	/// Max distance (yalms) to engage target before entering combat phase.
	/// Default <see cref="CombatDecision.DefaultEngageRange"/>.
	/// Clamped to
	/// [<see cref="CombatDecision.MinEngageRange"/>, <see cref="CombatDecision.MaxEngageRange"/>].
	/// Tanks / melee DPS use <see cref="CombatDecision.EffectiveEngageRange"/> (melee cap).
	/// </summary>
	public float EngageRange { get; set; } = CombatDecision.DefaultEngageRange;

	/// <summary>
	/// RSR <c>HostileType</c> setting applied on RotationAuto (AD default AllTargetsCanAttack).
	/// Clamped via <see cref="RsrSettingsDecision.ClampHostileType"/>.
	/// </summary>
	public RsrTargetHostileType RsrHostileType { get; set; } = RsrSettingsDecision.DefaultHostileType;

	/// <summary>
	/// RSR targeting when local player is a tank (AD default HighHP).
	/// Clamped via <see cref="RsrSettingsDecision.ClampTargetingType"/>.
	/// </summary>
	public RsrTargetingType RsrTargetingTank { get; set; } = RsrSettingsDecision.DefaultTankTargeting;

	/// <summary>
	/// RSR targeting when local player is not a tank (AD default LowHP).
	/// Clamped via <see cref="RsrSettingsDecision.ClampTargetingType"/>.
	/// </summary>
	public RsrTargetingType RsrTargetingNonTank { get; set; } = RsrSettingsDecision.DefaultNonTankTargeting;

	/// <summary>
	/// Enable BossMod / BossModReborn Automovement AI in hunt combat (dodge / safe zones).
	/// RSR remains GCD rotation; BM owns movement. Default on.
	/// </summary>
	public bool BossModIntegration { get; set; } = true;

	/// <summary>
	/// When both BossMod and BossModReborn are loaded, which to drive.
	/// Both share the <c>BossMod.*</c> CallGate prefix — prefer only one installed.
	/// </summary>
	public BossModPreference BossModPreference { get; set; } = BossModPreference.PreferBmr;

	/// <summary>Dalamud toast when a conductor hunt flag is received (TASKS 9.1).</summary>
	public bool EnableNotifications { get; set; } = true;

	/// <summary>Optional UI sound cue on conductor flag (TASKS 9.1).</summary>
	public bool EnableNotificationSound { get; set; } = false;

	/// <summary>
	/// Open the HuntAlerts-style alert info window when a HuntAlerts payload maps successfully.
	/// </summary>
	public bool ShowHuntAlertsInfoWindow { get; set; } = true;

	/// <summary>Record phase / follow / mount edges into the Debug tab ring buffer (TASKS 9.2).</summary>
	public bool EnableDebugLogging { get; set; } = true;

	/// <summary>
	/// Optional HuntAlerts IPC intake (TASKS 10.1). Default off — feature stays inert until enabled.
	/// </summary>
	public bool HuntAlertsIntegration { get; set; } = HuntAlertsFilter.DefaultIntegration;

	/// <summary>
	/// When HuntAlerts integration is on, parse conductor names from toast/IPC Message text and
	/// auto-add them to <see cref="Conductors"/>. Default off — HA free text is untrusted until
	/// the user opts in.
	/// </summary>
	public bool HuntAlertsAutoConductor { get; set; } = false;

	/// <summary>
	/// Worlds excluded from HuntAlerts auto-intake (HTA <c>WorldBlacklist</c> parity).
	/// Entries are world names and/or decimal RowIds; empty = no world filter.
	/// </summary>
	public List<string> HuntAlertsWorldBlacklist { get; set; } = [];

	/// <summary>
	/// Optional HuntAlerts rank gate. Empty = accept A-train (<c>new_hunt</c>) and S-rank (<c>srank</c>).
	/// Otherwise only listed <see cref="HuntMarkRank.A"/> / <see cref="HuntMarkRank.S"/>.
	/// </summary>
	public List<HuntMarkRank> HuntAlertsRankFilter { get; set; } = [];

	/// <summary>
	/// Optional HuntAlerts expansion / train-group gate (HuntAlerts <c>EnabledTrainGroups</c> names).
	/// Empty = all expansions. Otherwise only listed groups (Dawntrail, Endwalker, …).
	/// Resolved from start-territory <c>ExVersion</c> when known, else <c>huntKind</c>.
	/// </summary>
	public List<string> HuntAlertsTrainGroupFilter { get; set; } = [];
}
