#nullable enable

namespace HuntTrainAuto.HuntAlerts;

/// <summary>
/// Minimal HuntAlerts IPC payload for <c>HuntAlerts.OnHuntTrainMessageReceived</c>
/// (mirrors HuntAlerts <c>Helpers.HuntTrainMessage</c> fields needed for later mapping).
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
}
