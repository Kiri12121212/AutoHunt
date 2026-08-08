#nullable enable
using System;

namespace HuntTrainAuto.Tests;

public sealed class PluginVersionTests
{
	[Fact]
	public void FormatDisplay_uses_version_string()
		=> Assert.Equal("0.1.1.0", PluginVersion.FormatDisplay(new Version(0, 1, 1, 0)));

	[Fact]
	public void FormatDisplay_null_is_zero()
		=> Assert.Equal("0.0.0.0", PluginVersion.FormatDisplay(null));

	[Fact]
	public void FormatWindowTitle_includes_brand_and_version()
		=> Assert.Equal("HuntTrainAuto v0.1.1.0", PluginVersion.FormatWindowTitle("0.1.1.0"));

	[Fact]
	public void FormatStatusLine_prefixes_label()
		=> Assert.Equal("Plugin version: 0.1.1.0", PluginVersion.FormatStatusLine("0.1.1.0"));
}
