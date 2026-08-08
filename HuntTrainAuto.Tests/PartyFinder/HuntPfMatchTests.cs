#nullable enable
using System.Collections.Generic;

namespace HuntTrainAuto.Tests.PartyFinder;

public sealed class HuntPfMatchTests
{
	private static HuntPfListingInfo Hunt(
		ulong id,
		byte filled = 4,
		byte available = 8,
		byte searchArea = 0,
		string description = "",
		uint category = HuntPfMatch.TheHuntCategory)
		=> new()
		{
			Id = id,
			CategoryFlags = category,
			SlotsFilled = filled,
			SlotsAvailable = available,
			SearchAreaFlags = searchArea,
			Description = description,
			LeaderName = "Leader",
		};

	[Fact]
	public void IsSuitable_requires_hunt_category_open_slot_not_private()
	{
		Assert.True(HuntPfMatch.IsSuitable(Hunt(1)));
		Assert.False(HuntPfMatch.IsSuitable(Hunt(1, category: 0)));
		Assert.False(HuntPfMatch.IsSuitable(Hunt(1, filled: 8, available: 8)));
		Assert.False(HuntPfMatch.IsSuitable(Hunt(1, available: 0)));
		Assert.False(HuntPfMatch.IsSuitable(Hunt(1, searchArea: HuntPfMatch.PrivateSearchArea)));
		Assert.False(HuntPfMatch.IsSuitable(Hunt(0)));
	}

	[Fact]
	public void Score_prefers_fuller_parties_and_keywords()
	{
		var sparse = Hunt(1, filled: 1, description: "hello");
		var fuller = Hunt(2, filled: 6, description: "hello");
		var keyword = Hunt(3, filled: 1, description: "A-rank hunt train");
		Assert.True(HuntPfMatch.Score(in fuller) > HuntPfMatch.Score(in sparse));
		Assert.True(HuntPfMatch.Score(in keyword) > HuntPfMatch.Score(in sparse));
		Assert.Equal(int.MinValue, HuntPfMatch.Score(Hunt(9, category: 0)));
	}

	[Fact]
	public void PickBest_returns_null_when_empty_or_unsuitable()
	{
		Assert.Null(HuntPfMatch.PickBest([]));
		Assert.Null(HuntPfMatch.PickBest([Hunt(1, category: 0), Hunt(2, searchArea: HuntPfMatch.PrivateSearchArea)]));
	}

	[Fact]
	public void PickBest_chooses_highest_score_then_stable_id()
	{
		var a = Hunt(10, filled: 5, description: "train");
		var b = Hunt(20, filled: 7);
		var c = Hunt(5, filled: 7);
		var best = HuntPfMatch.PickBest([a, b, c]);
		Assert.NotNull(best);
		// b and c same filled; c has lower id and no keyword — score equal on filled only.
		// a has keyword but lower filled (5*10+3=53 vs 7*10=70).
		Assert.Equal(5uL, best.Value.Id);
	}

	[Fact]
	public void PickBest_ignores_full_and_non_hunt()
	{
		var list = new List<HuntPfListingInfo>
		{
			Hunt(1, filled: 8, available: 8),
			Hunt(2, category: HuntPfMatch.TheHuntCategory >> 1),
			Hunt(3, filled: 2, available: 8, description: "marks"),
		};
		var best = HuntPfMatch.PickBest(list);
		Assert.NotNull(best);
		Assert.Equal(3uL, best.Value.Id);
	}

	[Fact]
	public void Constants_match_client_structs_duty_category_bits()
	{
		Assert.Equal(1u << 11, HuntPfMatch.TheHuntCategory);
		Assert.Equal(11, HuntPfMatch.HuntCategoryTab);
		Assert.Equal(1, HuntPfMatch.WorldSearchAreaTab);
		Assert.Equal(1 << 1, HuntPfMatch.PrivateSearchArea);
	}
}
