#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace HuntTrainAuto.HuntAlerts;

/// <summary>
/// Coerce HuntAlerts IPC payloads into <see cref="HuntTrainMessage"/>.
/// CallGate JSON-converts across plugin assemblies. Prefer subscribing as
/// <see cref="HuntTrainMessage"/> so Newtonsoft fills fields directly.
/// When the gate still delivers a foreign CLR shape or a JSON dictionary
/// (<c>JObject</c> / <see cref="IDictionary"/>), copy members by name.
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
		return HasAnyMappedContent(message);
	}

	/// <summary>True when at least one hunt-identifying field was populated.</summary>
	public static bool HasAnyMappedContent(HuntTrainMessage message)
	{
		ArgumentNullException.ThrowIfNull(message);
		return !string.IsNullOrWhiteSpace(message.huntType)
		       || !string.IsNullOrWhiteSpace(message.huntWorld)
		       || message.startTerritoryTypeId != 0
		       || !string.IsNullOrWhiteSpace(message.locationCoords)
		       || message.mapLocationX != 0f
		       || message.mapLocationY != 0f
		       || !string.IsNullOrWhiteSpace(message.Message);
	}

	/// <summary>Copy public instance fields/properties (and dictionary keys) onto <paramref name="target"/> by name.</summary>
	public static void CopyByMemberName(object source, HuntTrainMessage target)
	{
		ArgumentNullException.ThrowIfNull(source);
		ArgumentNullException.ThrowIfNull(target);

		if (TryCopyFromDictionary(source, target))
			return;

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

	/// <summary>
	/// CallGate <c>DeserializeObject(..., typeof(object))</c> yields Newtonsoft
	/// <c>JObject</c> (an <see cref="IDictionary"/>). Copy string keys into the DTO.
	/// </summary>
	public static bool TryCopyFromDictionary(object source, HuntTrainMessage target)
	{
		ArgumentNullException.ThrowIfNull(source);
		ArgumentNullException.ThrowIfNull(target);

		switch (source)
		{
			case IDictionary<string, object?> generic:
				foreach (var pair in generic)
					TryAssign(target, pair.Key, UnwrapToken(pair.Value));
				return true;
			case IDictionary dictionary:
			{
				foreach (DictionaryEntry entry in dictionary)
				{
					if (entry.Key is not string name)
						continue;
					TryAssign(target, name, UnwrapToken(entry.Value));
				}

				return true;
			}
			default:
				return TryCopyFromKeyValueEnumerable(source, target);
		}
	}

	private static bool TryCopyFromKeyValueEnumerable(object source, HuntTrainMessage target)
	{
		// JObject also enumerates JProperty (Name/Value) without needing a Newtonsoft reference.
		if (source is string || source is not IEnumerable enumerable)
			return false;

		var copied = false;
		foreach (var item in enumerable)
		{
			if (item == null)
				continue;

			var itemType = item.GetType();
			var nameProp = itemType.GetProperty("Name", BindingFlags.Instance | BindingFlags.Public)
			               ?? itemType.GetProperty("Key", BindingFlags.Instance | BindingFlags.Public);
			var valueProp = itemType.GetProperty("Value", BindingFlags.Instance | BindingFlags.Public);
			if (nameProp == null || valueProp == null || nameProp.GetIndexParameters().Length > 0)
				continue;
			if (nameProp.GetValue(item) is not string name)
				continue;

			TryAssign(target, name, UnwrapToken(valueProp.GetValue(item)));
			copied = true;
		}

		return copied;
	}

	/// <summary>Flatten Newtonsoft <c>JValue</c> / boxed primitives to assignable CLR values.</summary>
	public static object? UnwrapToken(object? value)
	{
		if (value == null)
			return null;

		// JValue exposes .Value; avoid a hard Newtonsoft dependency in unit tests.
		var type = value.GetType();
		if (type.FullName is "Newtonsoft.Json.Linq.JValue" or "Newtonsoft.Json.Linq.JProperty")
		{
			var inner = type.GetProperty("Value", BindingFlags.Instance | BindingFlags.Public)
				?.GetValue(value);
			return UnwrapToken(inner);
		}

		if (type.FullName == "Newtonsoft.Json.Linq.JObject"
		    || type.FullName == "Newtonsoft.Json.Linq.JArray")
			return value.ToString();

		return value;
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
			case "HuntType":
				target.huntType = AsString(value) ?? target.huntType;
				break;
			case nameof(HuntTrainMessage.huntKind):
			case "HuntKind":
				target.huntKind = AsString(value) ?? target.huntKind;
				break;
			case nameof(HuntTrainMessage.huntWorld):
			case "HuntWorld":
				target.huntWorld = AsString(value) ?? target.huntWorld;
				break;
			case nameof(HuntTrainMessage.startLocation):
			case "StartLocation":
				target.startLocation = AsString(value) ?? target.startLocation;
				break;
			case nameof(HuntTrainMessage.startZone):
			case "StartZone":
				target.startZone = AsString(value) ?? target.startZone;
				break;
			case nameof(HuntTrainMessage.locationCoords):
			case "LocationCoords":
				target.locationCoords = AsString(value) ?? target.locationCoords;
				break;
			case nameof(HuntTrainMessage.startLocationAetheryteId):
			case "StartLocationAetheryteId":
			case "StartingAetheryteId":
				if (TryUInt(value, out var aetheryte))
					target.startLocationAetheryteId = aetheryte;
				break;
			case nameof(HuntTrainMessage.startTerritoryTypeId):
			case "StartTerritoryTypeId":
			case "StartingTerritoryTypeId":
				if (TryUInt(value, out var territory))
					target.startTerritoryTypeId = territory;
				break;
			case nameof(HuntTrainMessage.instance):
			case "Instance":
				if (TryInt(value, out var instance))
					target.instance = instance;
				break;
			case nameof(HuntTrainMessage.mapLocationX):
			case "MapLocationX":
				if (TryFloat(value, out var x))
					target.mapLocationX = x;
				break;
			case nameof(HuntTrainMessage.mapLocationY):
			case "MapLocationY":
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
			case long l when l >= int.MinValue && l <= int.MaxValue:
				result = (int)l;
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
			case decimal m:
				result = (float)m;
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
