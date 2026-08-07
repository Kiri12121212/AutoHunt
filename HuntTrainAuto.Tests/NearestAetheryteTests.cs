#nullable enable
using System.Collections.Generic;
using Xunit;

namespace HuntTrainAuto.Tests;

public sealed class NearestAetheryteTests
{
	[Fact]
	public void Select_picks_minimum_squared_distance()
	{
		var candidates = new[]
		{
			new NearestAetheryte.Candidate(1, "Far", 20f, 20f),
			new NearestAetheryte.Candidate(2, "Near", 11f, 10f),
			new NearestAetheryte.Candidate(3, "Mid", 15f, 12f),
		};

		var result = NearestAetheryte.Select(10f, 10f, candidates);
		Assert.NotNull(result);
		Assert.Equal(2u, result.Value.RowId);
		Assert.Equal("Near", result.Value.PlaceName);
	}

	[Fact]
	public void Select_skips_blacklist()
	{
		var candidates = new[]
		{
			new NearestAetheryte.Candidate(1, "Closest", 10f, 10f),
			new NearestAetheryte.Candidate(2, "Next", 12f, 10f),
		};

		var result = NearestAetheryte.Select(10f, 10f, candidates, blacklist: new HashSet<uint> { 1 });
		Assert.NotNull(result);
		Assert.Equal(2u, result.Value.RowId);
		Assert.Equal("Next", result.Value.PlaceName);
	}

	[Fact]
	public void Select_returns_null_when_all_blacklisted()
	{
		var candidates = new[]
		{
			new NearestAetheryte.Candidate(1, "A", 1f, 1f),
		};

		Assert.Null(NearestAetheryte.Select(0f, 0f, candidates, new HashSet<uint> { 1 }));
	}

	[Fact]
	public void Select_returns_null_when_empty()
	{
		Assert.Null(NearestAetheryte.Select(0f, 0f, System.Array.Empty<NearestAetheryte.Candidate>()));
	}

	[Fact]
	public void Select_applies_compensation_via_precomputed_coords()
	{
		// Macarenses is slightly closer without hack; +999 Y pushes it away so Plain wins.
		var plain = new NearestAetheryte.Candidate(10, "Plain", 12f, 10f);
		var macarensesNear = new NearestAetheryte.Candidate(11, "The Macarenses Angle", 10.5f, 10f);
		var macarensesHacked = new NearestAetheryte.Candidate(11, "The Macarenses Angle", 10.5f, 10f + 999f);

		var withoutHack = NearestAetheryte.Select(10f, 10f, [plain, macarensesNear]);
		Assert.Equal(11u, withoutHack!.Value.RowId);

		var withHack = NearestAetheryte.Select(10f, 10f, [plain, macarensesHacked]);
		Assert.Equal(10u, withHack!.Value.RowId);
	}

	[Fact]
	public void SquaredDistance_matches_pow_sum()
	{
		Assert.Equal(25.0, NearestAetheryte.SquaredDistance(0f, 0f, 3f, 4f));
	}

	[Fact]
	public void Compensation_plus_select_end_to_end()
	{
		var delta = DistanceCompensation.GetDelta("Tertium", enabled: true);
		var a = new NearestAetheryte.Candidate(1, "Tertium", 10f + delta.X, 15f + delta.Y); // (10, 10)
		var b = new NearestAetheryte.Candidate(2, "Other", 12f, 10f);

		// Flag at (10,10): Tertium after hack is exact; Other is farther.
		var result = NearestAetheryte.Select(10f, 10f, [a, b]);
		Assert.Equal(1u, result!.Value.RowId);
	}
}
