#nullable enable
using HuntTrainAuto.Chat;

namespace HuntTrainAuto.Tests.Chat;

public sealed class ConductorInstanceParseTests
{
	[Theory]
	[InlineData(null, 0)]
	[InlineData("", 0)]
	[InlineData("Labyrinthos flag", 0)]
	[InlineData("next", 0)]
	public void TryParse_unspecified(string? content, int expected)
		=> Assert.Equal(expected, ConductorInstanceParse.TryParse(content));

	[Theory]
	[InlineData("\uE0B1", 1)]
	[InlineData("\uE0B2", 2)]
	[InlineData("\uE0B3", 3)]
	[InlineData("\uE0B9", 9)]
	[InlineData("flag \uE0B2 Labyrinthos", 2)]
	public void TryParse_ffxiv_glyphs(string content, int expected)
		=> Assert.Equal(expected, ConductorInstanceParse.TryParse(content));

	[Theory]
	[InlineData("i1", 1)]
	[InlineData("I2", 2)]
	[InlineData("next i3 please", 3)]
	[InlineData("i9", 9)]
	[InlineData("hi2", 0)]
	[InlineData("i10", 0)]
	public void TryParse_ascii_iN(string content, int expected)
		=> Assert.Equal(expected, ConductorInstanceParse.TryParse(content));

	[Fact]
	public void TryParse_glyph_beats_ascii()
		=> Assert.Equal(2, ConductorInstanceParse.TryParse("i3 \uE0B2"));
}
