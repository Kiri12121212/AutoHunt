using System;
using System.Collections.Generic;
using Dalamud.Configuration;

namespace HuntTrainAuto;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
	public int Version { get; set; } = 1;

	public bool Enabled { get; set; } = true;
	public List<string> Conductors { get; set; } = [];
	public bool SuppressChatOtherPlayers { get; set; } = true;
	public bool ContextMenu { get; set; } = true;
	public bool AutoOpenMap { get; set; } = true;
	public bool NoDuplicateFlags { get; set; } = true;

	/// <summary>HTA parity: auto-teleport on conductor flags (zone / instance / far).</summary>
	public bool AutoTeleport { get; set; } = true;

	/// <summary>
	/// Same-zone skip threshold: if player distance to flag ≤ this, skip TP (mount/nav later).
	/// HTA default is 3f (<c>Config.AutoTeleportAetheryteDistanceDiff</c>); kept for parity.
	/// Units must match the distance passed into <see cref="TeleportDecision.Decide"/>.
	/// </summary>
	public float AutoTeleportAetheryteDistanceDiff { get; set; } = 3f;

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
	/// Party-stack follow distance (yalms). Default <see cref="FollowDecision.DefaultFollowDistance"/>.
	/// Effective value is clamped to
	/// [<see cref="FollowDecision.MinFollowDistance"/>, <see cref="FollowDecision.MaxFollowDistance"/>].
	/// </summary>
	public float PartyFollowDistance { get; set; } = FollowDecision.DefaultFollowDistance;
	public bool FollowConductorFirst { get; set; } = true;
}
