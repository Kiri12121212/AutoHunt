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

	/// <summary>Aetheryte RowIds excluded from nearest-aetheryte selection (HTA default included 148; we start empty).</summary>
	public List<uint> AetheryteBlacklist { get; set; } = [];

	/// <summary>HTA distance-compensation hack for named aetherytes. Default false (HTA parity).</summary>
	public bool DistanceCompensationHack { get; set; } = false;

	public bool UseMount { get; set; } = true;
	public int Mount { get; set; } = 0;

	public float PartyFollowDistance { get; set; } = 3f;
	public bool FollowConductorFirst { get; set; } = true;
}
