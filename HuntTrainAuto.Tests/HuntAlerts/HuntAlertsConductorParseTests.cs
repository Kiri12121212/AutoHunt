#nullable enable

namespace HuntTrainAuto.Tests.HuntAlerts;

public sealed class HuntAlertsConductorParseTests
{
	[Fact]
	public void TryExtract_dash_form_from_live_ha_history()
	{
		const string message =
			"""
			Kind: Hunt Train
			Hunt: Centurio
			Start Zone: The Fringes
			Aetheryte: Castrum Oriens
			World: Siren
			Posted: 01:34 AM

			###  Petri's Stormblood Train
			Gathering - **Castrum Oriens**, Siren
			Departing - in **~6min**
			Speed - always **Bullet**
			Marks - 11/12
			Conductor - Petrichor Daydream
			Scouts - Petri
			""";

		Assert.True(HuntAlertsConductorParse.TryExtract(message, out var name, out var world));
		Assert.Equal("Petrichor Daydream", name);
		Assert.Null(world);
	}

	[Fact]
	public void TryExtract_colon_with_bracket_world_from_live_ha_history()
	{
		const string message =
			"""
			Kind: Hunt Train
			Hunt: Dawntrail
			Start Zone: Heritage Found
			Aetheryte: Electrope Strike
			World: Raiden
			Posted: 01:46 AM

			**[Raiden]** Hunt train starting 01:55 AM at **Heritage Found INSTANCE 1 - The Outskirts** | N70 Train
			[Marks] DT: 14/14
			[Speed] FAST
			[Scout] (Conductor: [Twintania] Nelumy Teishi).
			""";

		Assert.True(HuntAlertsConductorParse.TryExtract(message, out var name, out var world));
		Assert.Equal("Nelumy Teishi", name);
		Assert.Equal("Twintania", world);
	}

	[Theory]
	[InlineData("Conductor: Alice Bob", "Alice Bob", null)]
	[InlineData("conductor: Alice Bob", "Alice Bob", null)]
	[InlineData("Conductor: Alice Bob @Ragnarok", "Alice Bob", "Ragnarok")]
	[InlineData("Conductor — Alice Bob", "Alice Bob", null)]
	[InlineData("**Conductor:** Alice Bob", "Alice Bob", null)]
	public void TryExtract_common_variants(string message, string expectedName, string? expectedWorld)
	{
		Assert.True(HuntAlertsConductorParse.TryExtract(message, out var name, out var world));
		Assert.Equal(expectedName, name);
		Assert.Equal(expectedWorld, world);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("Kind: Hunt Train\nWorld: Jenova\n\nNo conductor here")]
	[InlineData("Conductor's chat is loud")]
	[InlineData("Conductor soon")]
	[InlineData("Conductor - Soon")]
	[InlineData("Conductor - Very Fast")]
	[InlineData("Not Conductor: Alice Bob")]
	[InlineData("Former Conductor: Alice Bob")]
	public void TryExtract_rejects_missing_or_invalid(string? message)
		=> Assert.False(HuntAlertsConductorParse.TryExtract(message, out _, out _));

	[Fact]
	public void TryExtract_prefers_real_conductor_over_former_line()
	{
		const string message =
			"""
			Former Conductor: Alice Bob
			Conductor - Petrichor Daydream
			""";

		Assert.True(HuntAlertsConductorParse.TryExtract(message, out var name, out _));
		Assert.Equal("Petrichor Daydream", name);
	}
}
