#nullable enable
using HuntTrainAuto.Chat;

namespace HuntTrainAuto.Tests.Chat;

public sealed class SonarChatHintDecisionTests
{
	[Theory]
	[InlineData("Sonar", "Rank A <World> ", true, true)]
	[InlineData("Sonar", "Rank A killed ", true, false)]
	[InlineData("Alice", "Rank A", true, false)]
	[InlineData("Sonar", "Rank A", false, false)]
	[InlineData(null, "Rank A", true, false)]
	public void ShouldRememberHint(string? sender, string text, bool hasLink, bool expected)
		=> Assert.Equal(expected, SonarChatHintDecision.ShouldRememberHint(sender, text, hasLink));

	[Theory]
	[InlineData(true, "remember=true")]
	[InlineData(false, "remember=false")]
	public void Describe_reports_remember_outcome(bool shouldRemember, string expected)
		=> Assert.Equal(expected, SonarChatHintDecision.Describe(shouldRemember));
}
