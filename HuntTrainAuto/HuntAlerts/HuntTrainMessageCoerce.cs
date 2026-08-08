#nullable enable

using System;
using System.Globalization;
using System.Reflection;

namespace HuntTrainAuto.HuntAlerts;

/// <summary>
/// Coerce HuntAlerts IPC payloads into <see cref="HuntTrainMessage"/>.
/// CallGate JSON-converts across plugin assemblies; subscribing as
/// <c>object</c> keeps the publisher instance so we can copy public
/// fields/properties by name (HTA SonarMonitor field-shaped payloads).
/// </summary>
public static class HuntTrainMessageCoerce
{
	/// <summary>
	/// Map <paramref name="payload"/> to our IPC DTO.
	/// Same-type instances are returned as-is; foreign shapes are copied by member name.
	/// </summary>
	public static bool TryCoerce(object? payload, out HuntTrainMessage message)
	{
		message = null!;
		if (payload == null)
			return false;

		if (payload is HuntTrainMessage ours)
		{
			message = ours;
			return true;
		}

		message = new HuntTrainMessage();
		CopyByMemberName(payload, message);
		return true;
	}

	/// <summary>Copy public instance fields/properties from <paramref name="source"/> onto <paramref name="target"/> by name.</summary>
	public static void CopyByMemberName(object source, HuntTrainMessage target)
	{
		ArgumentNullException.ThrowIfNull(source);
		ArgumentNullException.ThrowIfNull(target);

		var srcType = source.GetType();
		foreach (var field in srcType.GetFields(BindingFlags.Instance | BindingFlags.Public))
			TryAssign(target, field.Name, field.GetValue(source));

		foreach (var prop in srcType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
		{
			if (!prop.CanRead || prop.GetIndexParameters().Length > 0)
				continue;
			TryAssign(target, prop.Name, prop.GetValue(source));
		}
	}

	private static void TryAssign(HuntTrainMessage target, string name, object? value)
	{
		if (value == null || string.IsNullOrEmpty(name))
			return;

		switch (name)
		{
			case nameof(HuntTrainMessage.Message):
				target.Message = AsString(value) ?? target.Message;
				break;
			case nameof(HuntTrainMessage.huntType):
				target.huntType = AsString(value) ?? target.huntType;
				break;
			case nameof(HuntTrainMessage.huntKind):
				target.huntKind = AsString(value) ?? target.huntKind;
				break;
			case nameof(HuntTrainMessage.huntWorld):
				target.huntWorld = AsString(value) ?? target.huntWorld;
				break;
			case nameof(HuntTrainMessage.startLocation):
				target.startLocation = AsString(value) ?? target.startLocation;
				break;
			case nameof(HuntTrainMessage.startZone):
				target.startZone = AsString(value) ?? target.startZone;
				break;
			case nameof(HuntTrainMessage.locationCoords):
				target.locationCoords = AsString(value) ?? target.locationCoords;
				break;
			case nameof(HuntTrainMessage.startLocationAetheryteId):
				if (TryUInt(value, out var aetheryte))
					target.startLocationAetheryteId = aetheryte;
				break;
			case nameof(HuntTrainMessage.startTerritoryTypeId):
				if (TryUInt(value, out var territory))
					target.startTerritoryTypeId = territory;
				break;
			case nameof(HuntTrainMessage.instance):
				if (TryInt(value, out var instance))
					target.instance = instance;
				break;
			case nameof(HuntTrainMessage.mapLocationX):
				if (TryFloat(value, out var x))
					target.mapLocationX = x;
				break;
			case nameof(HuntTrainMessage.mapLocationY):
				if (TryFloat(value, out var y))
					target.mapLocationY = y;
				break;
		}
	}

	private static string? AsString(object value)
		=> value as string ?? Convert.ToString(value, CultureInfo.InvariantCulture);

	private static bool TryUInt(object value, out uint result)
	{
		switch (value)
		{
			case uint u:
				result = u;
				return true;
			case int i when i >= 0:
				result = (uint)i;
				return true;
			case long l when l >= 0 && l <= uint.MaxValue:
				result = (uint)l;
				return true;
			case string s when uint.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out result):
				return true;
			default:
				try
				{
					result = Convert.ToUInt32(value, CultureInfo.InvariantCulture);
					return true;
				}
				catch
				{
					result = 0;
					return false;
				}
		}
	}

	private static bool TryInt(object value, out int result)
	{
		switch (value)
		{
			case int i:
				result = i;
				return true;
			case uint u when u <= int.MaxValue:
				result = (int)u;
				return true;
			case string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out result):
				return true;
			default:
				try
				{
					result = Convert.ToInt32(value, CultureInfo.InvariantCulture);
					return true;
				}
				catch
				{
					result = 0;
					return false;
				}
		}
	}

	private static bool TryFloat(object value, out float result)
	{
		switch (value)
		{
			case float f:
				result = f;
				return true;
			case double d:
				result = (float)d;
				return true;
			case string s when float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out result):
				return true;
			default:
				try
				{
					result = Convert.ToSingle(value, CultureInfo.InvariantCulture);
					return true;
				}
				catch
				{
					result = 0f;
					return false;
				}
		}
	}
}
