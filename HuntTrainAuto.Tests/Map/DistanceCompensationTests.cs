#nullable enable
using System.Numerics;

namespace HuntTrainAuto.Tests.Map;

public sealed class DistanceCompensationTests
{
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("Tertium")]
	[InlineData("Base Omicron")]
	public void Disabled_returns_zero(string? name)
	{
		Assert.Equal(Vector2.Zero, DistanceCompensation.GetDelta(name, enabled: false));
	}

	[Fact]
	public void Unknown_name_returns_zero_when_enabled()
	{
		Assert.Equal(Vector2.Zero, DistanceCompensation.GetDelta("Limsa Lominsa", enabled: true));
	}

	[Fact]
	public void Tertium_Y_minus_5()
	{
		Assert.Equal(new Vector2(0f, -5f), DistanceCompensation.GetDelta("Tertium", true));
	}

	[Fact]
	public void BaseOmicron_X_plus_5()
	{
		Assert.Equal(new Vector2(5f, 0f), DistanceCompensation.GetDelta("Base Omicron", true));
	}

	[Fact]
	public void BestwaysBurrow_delta()
	{
		Assert.Equal(new Vector2(-2f, -3f), DistanceCompensation.GetDelta("Bestways Burrow", true));
	}

	[Fact]
	public void TheGreatWork_Y_minus_2()
	{
		Assert.Equal(new Vector2(0f, -2f), DistanceCompensation.GetDelta("The Great Work", true));
	}

	[Fact]
	public void MacarensesAngle_Y_plus_999()
	{
		Assert.Equal(new Vector2(0f, 999f), DistanceCompensation.GetDelta("The Macarenses Angle", true));
	}

	[Fact]
	public void Names_are_case_sensitive_HTA_parity()
	{
		Assert.Equal(Vector2.Zero, DistanceCompensation.GetDelta("tertium", true));
	}
}
