#nullable enable

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
