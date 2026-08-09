#nullable enable
using System.Collections.Generic;

namespace HuntTrainAuto.Tests.Map;

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
		Assert.Equal(11f, result.Value.MapX);
		Assert.Equal(10f, result.Value.MapY);
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
	public void SquaredDistance_matches_pow_sum()
	{
		Assert.Equal(25.0, NearestAetheryte.SquaredDistance(0f, 0f, 3f, 4f));
	}

	[Fact]
	public void Describe_formats_pick_and_missing_result()
	{
		var picked = new NearestAetheryteResult(12, "Limsa", 10.125f, 20.5f);

		Assert.Equal("picked #12 (Limsa) at (10.13, 20.50)", picked.Describe());
		Assert.Equal("picked #12 (Limsa) at (10.13, 20.50)", NearestAetheryte.Describe(picked));
		Assert.Equal("no eligible aetheryte", NearestAetheryte.Describe(null));
	}
}
