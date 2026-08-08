#nullable enable

using HuntTrainAuto.HuntAlerts;

namespace HuntTrainAuto.Tests.HuntAlerts;

public sealed class HuntTrainMessageCoerceTests
{
	[Fact]
	public void TryCoerce_null_fails()
		=> Assert.False(HuntTrainMessageCoerce.TryCoerce(null, out _));

	[Fact]
	public void TryCoerce_same_type_returns_instance()
	{
		var src = new HuntTrainMessage
		{
			huntType = HuntAlertsFilter.HuntTypeATrain,
			huntWorld = "Ragnarok",
			startTerritoryTypeId = 1191,
		};
		Assert.True(HuntTrainMessageCoerce.TryCoerce(src, out var coerced));
		Assert.Same(src, coerced);
	}

	[Fact]
	public void TryCoerce_copies_public_fields_from_foreign_shape()
	{
		var foreign = new ForeignHuntPayload
		{
			huntType = HuntAlertsFilter.HuntTypeATrain,
			huntKind = "Dawntrail",
			huntWorld = "Ragnarok",
			startTerritoryTypeId = 1191,
			mapLocationX = 17.1f,
			mapLocationY = 23.9f,
			locationCoords = "17.1, 23.9",
			startLocationAetheryteId = 99,
			instance = 2,
			startLocation = "Yyasulani Station",
			startZone = "Heritage Found",
		};

		Assert.True(HuntTrainMessageCoerce.TryCoerce(foreign, out var msg));
		Assert.Equal(HuntAlertsFilter.HuntTypeATrain, msg.huntType);
		Assert.Equal("Dawntrail", msg.huntKind);
		Assert.Equal("Ragnarok", msg.huntWorld);
		Assert.Equal(1191u, msg.startTerritoryTypeId);
		Assert.Equal(17.1f, msg.mapLocationX);
		Assert.Equal(23.9f, msg.mapLocationY);
		Assert.Equal("17.1, 23.9", msg.locationCoords);
		Assert.Equal(99u, msg.startLocationAetheryteId);
		Assert.Equal(2, msg.instance);
		Assert.Equal("Yyasulani Station", msg.startLocation);
		Assert.Equal("Heritage Found", msg.startZone);
	}

	[Fact]
	public void TryCoerce_copies_public_properties_from_foreign_shape()
	{
		var foreign = new ForeignHuntProps
		{
			huntType = HuntAlertsFilter.HuntTypeSRank,
			huntWorld = "Phoenix",
			startTerritoryTypeId = 813,
			mapLocationX = 1.5f,
			mapLocationY = 2.5f,
		};

		Assert.True(HuntTrainMessageCoerce.TryCoerce(foreign, out var msg));
		Assert.Equal(HuntAlertsFilter.HuntTypeSRank, msg.huntType);
		Assert.Equal("Phoenix", msg.huntWorld);
		Assert.Equal(813u, msg.startTerritoryTypeId);
		Assert.Equal(1.5f, msg.mapLocationX);
		Assert.Equal(2.5f, msg.mapLocationY);
	}

	// Field-shaped like HuntAlerts.Helpers.HuntTrainMessage (no shared assembly type).
	private sealed class ForeignHuntPayload
	{
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

	private sealed class ForeignHuntProps
	{
		public string huntType { get; set; } = "";
		public string huntWorld { get; set; } = "";
		public uint startTerritoryTypeId { get; set; }
		public float mapLocationX { get; set; }
		public float mapLocationY { get; set; }
	}
}
