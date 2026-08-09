#nullable enable
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace HuntTrainAuto.Map;

/// <summary>
/// Localized unsafe read/write of the AgentMap flag marker (HTA <c>FlagMapMarker</c> /
/// <c>IsFlagMarkerSet</c>). Modern ClientStructs: <c>FlagMarkerCount</c> +
/// <c>FlagMapMarkers[0]</c> / <c>SetFlagMapMarker</c>.
/// </summary>
internal static class AgentMapFlag
{
	/// <summary>Default flag pin icon (same as game map-flag / SetFlagMapMarker default).</summary>
	public const uint DefaultFlagIconId = 60561;

	public static unsafe bool TryGet(out uint territoryId, out float x, out float y)
	{
		territoryId = 0;
		x = 0f;
		y = 0f;

		var agent = AgentMap.Instance();
		if (agent == null || agent->FlagMarkerCount == 0)
			return false;

		ref readonly var flag = ref agent->FlagMapMarkers[0];
		territoryId = flag.TerritoryId;
		x = flag.XFloat;
		y = flag.YFloat;
		return true;
	}

	/// <summary>
	/// Places the player flag pin at world XZ (same units as <see cref="TryGet"/> XFloat/YFloat).
	/// Soft-fails when agent/map ids unavailable.
	/// </summary>
	public static unsafe bool TrySet(
		uint territoryId,
		uint mapId,
		float worldX,
		float worldZ,
		uint iconId = DefaultFlagIconId)
	{
		if (territoryId == 0 || mapId == 0)
			return false;

		var agent = AgentMap.Instance();
		if (agent == null)
			return false;

		agent->SetFlagMapMarker(territoryId, mapId, worldX, worldZ, iconId);
		return true;
	}
}
