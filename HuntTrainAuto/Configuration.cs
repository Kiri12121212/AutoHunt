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

	/// <summary>Aetheryte RowIds excluded from nearest-aetheryte selection (HTA default included 148; we start empty).</summary>
	public List<uint> AetheryteBlacklist { get; set; } = [];

	/// <summary>HTA distance-compensation hack for named aetherytes. Default false (HTA parity).</summary>
	public bool DistanceCompensationHack { get; set; } = false;

	public bool UseMount { get; set; } = true;
	public int Mount { get; set; } = 0;

	public float PartyFollowDistance { get; set; } = 3f;
	public bool FollowConductorFirst { get; set; } = true;
}
