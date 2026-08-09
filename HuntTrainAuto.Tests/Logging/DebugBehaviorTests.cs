#nullable enable

namespace HuntTrainAuto.Tests.Logging;

public sealed class DebugBehaviorTests
{
	[Theory]
	[InlineData("Teleport", "skip AlreadyClose", "[Teleport] skip AlreadyClose")]
	[InlineData("", "bare", "bare")]
	[InlineData("  Chat  ", "ok", "[Chat] ok")]
	public void Format_prefixes_area(string area, string message, string expected)
		=> Assert.Equal(expected, DebugBehavior.Format(area, message));
}
