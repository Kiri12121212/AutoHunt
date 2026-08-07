#nullable enable

using System;
using HuntTrainAuto.Domain;

namespace HuntTrainAuto.Tests.HuntAlerts;

public sealed class HuntAlertsAvailabilityTests
{
	[Fact]
	public void Channel_names_match_HuntAlerts_IPCManager()
	{
		Assert.Equal(
			"HuntAlerts.OnHuntTrainMessageReceived",
			HuntAlertsAvailability.OnHuntTrainMessageReceivedChannel);
		Assert.Equal(
			"HuntAlerts.OnHuntAlertMessageReceived",
			HuntAlertsAvailability.OnHuntAlertMessageReceivedChannel);
		Assert.Equal("HuntAlerts", HuntAlertsAvailability.PluginInternalName);
		Assert.Equal(new Version(1, 2, 1, 3), HuntAlertsAvailability.MinimumVersion);
	}

	[Fact]
	public void IsPluginLoaded_true_when_HuntAlerts_loaded()
	{
		(string InternalName, bool IsLoaded)[] plugins =
		[
			("Other", true),
			("HuntAlerts", true),
		];
		Assert.True(HuntAlertsAvailability.IsPluginLoaded(plugins));
	}

	[Fact]
	public void IsPluginLoaded_false_when_missing_or_unloaded()
	{
		Assert.False(HuntAlertsAvailability.IsPluginLoaded([("HuntAlerts", false)]));
		Assert.False(HuntAlertsAvailability.IsPluginLoaded([("OtherPlugin", true)]));
		Assert.False(HuntAlertsAvailability.IsPluginLoaded([]));
	}

	[Theory]
	[InlineData("1.2.1.3", true)]
	[InlineData("1.2.1.4", true)]
	[InlineData("2.0.0.0", true)]
	[InlineData("1.2.1.2", false)]
	[InlineData("1.0.0.0", false)]
	public void MeetsMinimumVersion_compares_semver(string version, bool expected)
		=> Assert.Equal(expected, HuntAlertsAvailability.MeetsMinimumVersion(Version.Parse(version)));

	[Fact]
	public void MeetsMinimumVersion_false_when_null()
		=> Assert.False(HuntAlertsAvailability.MeetsMinimumVersion(null));

	[Fact]
	public void Evaluate_available_when_loaded_and_version_ok()
	{
		(string, bool, Version?)[] plugins =
		[
			("HuntAlerts", true, new Version(1, 2, 1, 3)),
		];
		Assert.Equal(HuntAlertsPluginStatus.Available, HuntAlertsAvailability.Evaluate(plugins));
		Assert.True(HuntAlertsAvailability.IsAvailable(plugins));
	}

	[Fact]
	public void Evaluate_outdated_when_loaded_below_minimum()
	{
		(string, bool, Version?)[] plugins =
		[
			("HuntAlerts", true, new Version(1, 2, 0, 0)),
		];
		Assert.Equal(HuntAlertsPluginStatus.Outdated, HuntAlertsAvailability.Evaluate(plugins));
		Assert.False(HuntAlertsAvailability.IsAvailable(plugins));
	}

	[Fact]
	public void Evaluate_missing_when_unloaded_or_absent()
	{
		Assert.Equal(
			HuntAlertsPluginStatus.Missing,
			HuntAlertsAvailability.Evaluate([("HuntAlerts", false, new Version(9, 0, 0, 0))]));
		Assert.Equal(
			HuntAlertsPluginStatus.Missing,
			HuntAlertsAvailability.Evaluate([]));
	}

	[Fact]
	public void Evaluate_outdated_when_loaded_with_null_version()
	{
		Assert.Equal(
			HuntAlertsPluginStatus.Outdated,
			HuntAlertsAvailability.Evaluate([("HuntAlerts", true, null)]));
	}

	[Theory]
	[InlineData(HuntAlertsPluginStatus.Available, "available")]
	[InlineData(HuntAlertsPluginStatus.Missing, "missing")]
	[InlineData(HuntAlertsPluginStatus.Outdated, "outdated")]
	public void StatusLabel_matches_state(HuntAlertsPluginStatus status, string expected)
		=> Assert.Equal(expected, HuntAlertsAvailability.StatusLabel(status));

	[Theory]
	[InlineData(HuntAlertsPluginStatus.Available, "HuntAlerts: available")]
	[InlineData(HuntAlertsPluginStatus.Missing, "HuntAlerts: missing")]
	[InlineData(HuntAlertsPluginStatus.Outdated, "HuntAlerts: outdated (1.2.1.3+)")]
	public void FormatAvailabilityLine_matches_HTA_indicator(HuntAlertsPluginStatus status, string expected)
		=> Assert.Equal(expected, HuntAlertsAvailability.FormatAvailabilityLine(status));

	[Fact]
	public void SafeEvaluate_swallows_probe_exceptions()
		=> Assert.Equal(
			HuntAlertsPluginStatus.Missing,
			HuntAlertsAvailability.SafeEvaluate(
				() => throw new InvalidOperationException("InstalledPlugins blew up")));

	[Fact]
	public void SafeEvaluate_returns_probe_result()
		=> Assert.Equal(
			HuntAlertsPluginStatus.Available,
			HuntAlertsAvailability.SafeEvaluate(() => HuntAlertsPluginStatus.Available));

	[Fact]
	public void FormatLastAlertStatus_none_when_null()
		=> Assert.Equal("Last alert: none", HuntAlertsAvailability.FormatLastAlertStatus(null));

	[Fact]
	public void FormatLastAlertStatus_includes_world_place_and_time()
	{
		var last = new HuntAlertsLastAlert(
			new DateTimeOffset(2026, 8, 8, 14, 32, 5, TimeSpan.Zero),
			"Phoenix",
			"Labyrinthos",
			123);
		Assert.Equal(
			"Last alert: Phoenix / Labyrinthos @ 14:32:05 UTC",
			HuntAlertsAvailability.FormatLastAlertStatus(last));
	}

	[Fact]
	public void FormatLastAlertStatus_falls_back_to_territory()
	{
		var last = new HuntAlertsLastAlert(
			new DateTimeOffset(2026, 8, 8, 1, 2, 3, TimeSpan.Zero),
			null,
			null,
			456);
		Assert.Equal(
			"Last alert: Territory 456 @ 01:02:03 UTC",
			HuntAlertsAvailability.FormatLastAlertStatus(last));
	}

	[Fact]
	public void FromMappedFlag_copies_summary_fields()
	{
		var flag = HuntFlag.FromMapLink(10, 20, 1, 2, "Somewhere", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
		flag.HuntWorld = "Ragnarok";
		var last = HuntAlertsAvailability.FromMappedFlag(flag);
		Assert.Equal(flag.Timestamp, last.Timestamp);
		Assert.Equal("Ragnarok", last.World);
		Assert.Equal("Somewhere", last.PlaceName);
		Assert.Equal(10u, last.TerritoryTypeId);
	}

	[Theory]
	[InlineData(true, true, true)]
	[InlineData(true, false, false)]
	[InlineData(false, true, false)]
	[InlineData(false, false, false)]
	public void ShouldHandle_requires_integration_and_plugin(
		bool integration,
		bool pluginLoaded,
		bool expected)
		=> Assert.Equal(
			expected,
			HuntAlertsAvailability.ShouldHandle(integration, pluginLoaded));

	[Fact]
	public void TryAcceptMessage_no_op_when_integration_off()
	{
		var message = new HuntTrainMessage { huntType = HuntAlertsFilter.HuntTypeATrain };
		Assert.False(HuntAlertsAvailability.TryAcceptMessage(
			huntAlertsIntegration: false,
			pluginLoaded: true,
			message,
			out _));
	}

	[Fact]
	public void TryAcceptMessage_no_op_when_plugin_missing()
	{
		var message = new HuntTrainMessage { huntType = HuntAlertsFilter.HuntTypeSRank };
		Assert.False(HuntAlertsAvailability.TryAcceptMessage(
			huntAlertsIntegration: true,
			pluginLoaded: false,
			message,
			out _));
	}

	[Fact]
	public void TryAcceptMessage_no_op_when_message_null()
	{
		Assert.False(HuntAlertsAvailability.TryAcceptMessage(
			huntAlertsIntegration: true,
			pluginLoaded: true,
			message: null,
			out _));
	}

	[Fact]
	public void TryAcceptMessage_forwards_when_enabled_and_loaded()
	{
		var message = new HuntTrainMessage
		{
			huntType = HuntAlertsFilter.HuntTypeATrain,
			huntWorld = "Phoenix",
			startTerritoryTypeId = 123,
		};

		Assert.True(HuntAlertsAvailability.TryAcceptMessage(
			huntAlertsIntegration: true,
			pluginLoaded: true,
			message,
			out var accepted));
		Assert.Same(message, accepted);
	}
}
