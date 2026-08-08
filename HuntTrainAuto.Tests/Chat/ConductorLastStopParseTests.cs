#nullable enable
using HuntTrainAuto.Chat;

namespace HuntTrainAuto.Tests.Chat;

public sealed class ConductorLastStopParseTests
{
	[Theory]
	[InlineData(null, false)]
	[InlineData("", false)]
	[InlineData("next flag soon", false)]
	[InlineData("LAST STOP", true)]
	[InlineData("last stop guys", true)]
	[InlineData("<< LAST  STOP >>", true)]
	[InlineData("blast stopped", false)]
	public void IsLastStop(string? content, bool expected)
		=> Assert.Equal(expected, ConductorLastStopParse.IsLastStop(content));
}
