#nullable enable
using System.Numerics;
using HuntTrainAuto.Combat;
using HuntTrainAuto.Domain;

namespace HuntTrainAuto.Tests.Combat;

public sealed class EngagePositionHintTests
{
	[Fact]
	public void RememberFromFlag_uses_world_pos_when_set()
	{
		var flag = HuntFlag.FromMapLink(100, 1, 12000, 34000, "Test");
		flag.WorldPos = new Vector3(12f, 5f, 34f);
		var hint = new EngagePositionHint();
		hint.RememberFromFlag(flag, EngagePositionHintSource.HuntAlerts);
		Assert.True(hint.HasHint);
		Assert.Equal(EngagePositionHintSource.HuntAlerts, hint.Source);
		Assert.Equal(new Vector3(12f, 5f, 34f), hint.WorldPos);
		Assert.Equal(100u, hint.TerritoryTypeId);
	}

	[Fact]
	public void RememberFromFlag_approximates_raw_when_no_floor()
	{
		var flag = HuntFlag.FromMapLink(100, 1, 12000, 34000, "Test");
		var hint = new EngagePositionHint();
		hint.RememberFromFlag(flag, EngagePositionHintSource.ConductorFlag);
		Assert.Equal(new Vector3(12f, 0f, 34f), hint.WorldPos);
	}

	[Fact]
	public void Describe_formats_stored_hint()
	{
		var hint = new EngagePositionHint();
		hint.Remember(new Vector3(12f, 5f, 34f), 100, EngagePositionHintSource.HuntAlerts);

		Assert.Equal(
			"hint=HuntAlerts, territory=100, position=(12.0,5.0,34.0)",
			hint.Describe());
	}

	[Fact]
	public void Describe_formats_absent_hint()
		=> Assert.Equal("hint=none", new EngagePositionHint().Describe());

	[Fact]
	public void WorldPosForTerritory_rejects_other_zone()
	{
		var hint = new EngagePositionHint();
		hint.Remember(new Vector3(1f, 0f, 2f), 100, EngagePositionHintSource.SonarChat);
		Assert.Null(hint.WorldPosForTerritory(200));
		Assert.Equal(new Vector3(1f, 0f, 2f), hint.WorldPosForTerritory(100));
	}

	[Fact]
	public void DistanceXZ_ignores_y()
	{
		var d = EngagePositionHint.DistanceXZ(
			new Vector3(0f, 100f, 0f),
			new Vector3(3f, 0f, 4f));
		Assert.Equal(5f, d, precision: 3);
	}
}
