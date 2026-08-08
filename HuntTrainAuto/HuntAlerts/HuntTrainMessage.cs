#nullable enable

namespace HuntTrainAuto.HuntAlerts;

/// <summary>
/// Minimal HuntAlerts IPC payload for <c>HuntAlerts.OnHuntTrainMessageReceived</c>
/// (mirrors HuntAlerts <c>Helpers.HuntTrainMessage</c> fields needed for mapping + alert UI).
/// </summary>
public sealed class HuntTrainMessage
{
	public string Message = "";
	public string huntType = "";
	public string huntKind = "";
	public string huntWorld = "";
	public string startLocation = "";
	public uint startLocationAetheryteId;
	public string startZone = "";
	public int instance;
	public string locationCoords = "";
	public uint startTerritoryTypeId;
	public float mapLocationX;
	public float mapLocationY;

	/// <summary>Local posted clock string from HuntAlerts (e.g. <c>11:36 PM</c>).</summary>
	public string Posted_Time = "";

	/// <summary>Unix seconds when HuntAlerts posted the alert (0 when unknown).</summary>
	public long PostedEpoch;

	/// <summary>S-rank creature name when HuntAlerts provides it.</summary>
	public string creatureName = "";

	/// <summary>Shallow copy for UI retention (CallGate payloads must not mutate the snapshot).</summary>
	public HuntTrainMessage Clone()
		=> new()
		{
			Message = Message,
			huntType = huntType,
			huntKind = huntKind,
			huntWorld = huntWorld,
			startLocation = startLocation,
			startLocationAetheryteId = startLocationAetheryteId,
			startZone = startZone,
			instance = instance,
			locationCoords = locationCoords,
			startTerritoryTypeId = startTerritoryTypeId,
			mapLocationX = mapLocationX,
			mapLocationY = mapLocationY,
			Posted_Time = Posted_Time,
			PostedEpoch = PostedEpoch,
			creatureName = creatureName,
		};
}
