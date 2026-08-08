#nullable enable
using System;
using System.Reflection;

namespace HuntTrainAuto;

/// <summary>
/// Plugin assembly version helpers for config UI and ship workflow.
/// Source of truth at build time: csproj <c>Version</c> (synced to manifest AssemblyVersion).
/// </summary>
public static class PluginVersion
{
	public static Version? ReadAssemblyVersion(Assembly? assembly = null)
		=> (assembly ?? typeof(PluginVersion).Assembly).GetName().Version;

	public static string FormatDisplay(Version? version)
		=> version?.ToString() ?? "0.0.0.0";

	public static string DisplayString
		=> FormatDisplay(ReadAssemblyVersion());

	public static string FormatWindowTitle(string version)
		=> $"HuntTrainAuto v{version}";

	public static string WindowTitle
		=> FormatWindowTitle(DisplayString);

	public static string FormatStatusLine(string version)
		=> $"Plugin version: {version}";

	public static string StatusLine
		=> FormatStatusLine(DisplayString);
}
