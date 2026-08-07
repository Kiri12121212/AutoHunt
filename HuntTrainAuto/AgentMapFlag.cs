#nullable enable
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace HuntTrainAuto;

/// <summary>
/// Localized unsafe read of the current AgentMap flag marker (HTA <c>FlagMapMarker</c> / <c>IsFlagMarkerSet</c>).
/// Modern ClientStructs: <c>FlagMarkerCount</c> + <c>FlagMapMarkers[0]</c>.
/// </summary>
internal static class AgentMapFlag
{
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
}
