#nullable enable

using System;
using HuntTrainAuto.Domain;
using HuntTrainAuto.HuntAlerts;
using HuntTrainAuto.Map;

namespace HuntTrainAuto.Tests.HuntAlerts;

public sealed class HuntAlertsFlagDedupeTests
{
	private static readonly DateTimeOffset T0 = DateTimeOffset.UnixEpoch;

	private static HuntFlag Flag(uint territory, int rawX, int rawY)
		=> HuntFlag.FromMapLink(territory, 1, rawX, rawY, "x", T0);

	private static HuntFlagDedupeMemory Mem(
		HuntFlag flag,
		HuntFlagIntakeSource source,
		DateTimeOffset? at = null)
		=> HuntAlertsFlagDedupe.Remember(flag, source, at ?? T0);

	[Fact]
	public void IsNearDuplicate_false_when_no_active()
		=> Assert.False(HuntAlertsFlagDedupe.IsNearDuplicate(null, Flag(813, 0, 0)));

	[Fact]
	public void IsNearDuplicate_false_when_different_territory()
	{
		var active = Flag(813, 0, 0);
		Assert.False(HuntAlertsFlagDedupe.IsNearDuplicate(active, Flag(814, 0, 0)));
	}

	[Fact]
	public void IsNearDuplicate_true_when_within_threshold()
	{
		// Scaled distance: |5000|/1000 = 5 < 10
		var active = Flag(813, 0, 0);
		Assert.True(HuntAlertsFlagDedupe.IsNearDuplicate(active, Flag(813, 5000, 0)));
	}

	[Fact]
	public void IsNearDuplicate_false_when_beyond_threshold()
	{
		// Scaled distance: 15000/1000 = 15 > 10
		var active = Flag(813, 0, 0);
		Assert.False(HuntAlertsFlagDedupe.IsNearDuplicate(active, Flag(813, 15_000, 0)));
	}

	[Fact]
	public void ShouldSuppress_only_when_pipeline_active_and_near_dup()
	{
		var active = Flag(813, 0, 0);
		var near = Flag(813, 1000, 0);
		var far = Flag(813, 20_000, 0);

		Assert.True(HuntAlertsFlagDedupe.ShouldSuppress(active, near, pipelineActive: true));
		Assert.False(HuntAlertsFlagDedupe.ShouldSuppress(active, near, pipelineActive: false));
		Assert.False(HuntAlertsFlagDedupe.ShouldSuppress(active, far, pipelineActive: true));
	}

	[Fact]
	public void ShouldSuppress_false_when_forceAccept_even_if_near_dup_pipeline_active()
	{
		var active = Flag(813, 0, 0);
		var near = Flag(813, 1000, 0);

		Assert.True(HuntAlertsFlagDedupe.ShouldSuppress(active, near, pipelineActive: true));
		Assert.False(HuntAlertsFlagDedupe.ShouldSuppress(
			active, near, pipelineActive: true, forceAccept: true));
	}

	[Fact]
	public void ShouldProceedAbortVisitThenEnter_false_when_near_dup_would_suppress()
	{
		var active = Flag(813, 0, 0);
		var near = Flag(813, 1000, 0);

		Assert.False(HuntAlertsFlagDedupe.ShouldProceedAbortVisitThenEnter(
			active, near, pipelineActive: true));
	}

	[Fact]
	public void ShouldProceedAbortVisitThenEnter_true_when_accept_would_run()
	{
		var active = Flag(813, 0, 0);
		var far = Flag(813, 20_000, 0);
		var nearIdle = Flag(813, 1000, 0);

		Assert.True(HuntAlertsFlagDedupe.ShouldProceedAbortVisitThenEnter(
			active, far, pipelineActive: true));
		Assert.True(HuntAlertsFlagDedupe.ShouldProceedAbortVisitThenEnter(
			active, nearIdle, pipelineActive: false));
		Assert.True(HuntAlertsFlagDedupe.ShouldProceedAbortVisitThenEnter(
			null, nearIdle, pipelineActive: true));
	}

	[Fact]
	public void DefaultCrossSourceWindow_is_thirty_seconds()
		=> Assert.Equal(TimeSpan.FromSeconds(30), HuntAlertsFlagDedupe.DefaultCrossSourceWindow);

	[Fact]
	public void ShouldSuppressCrossSource_chat_then_ha_within_window()
	{
		var chat = Flag(813, 0, 0);
		var ha = Flag(813, 2000, 0);
		var mem = Mem(chat, HuntFlagIntakeSource.Chat, T0);

		Assert.True(HuntAlertsFlagDedupe.ShouldSuppressCrossSource(
			mem,
			ha,
			HuntFlagIntakeSource.HuntAlerts,
			T0.AddSeconds(10),
			huntAlertsIntegration: true));
	}

	[Fact]
	public void ShouldSuppressCrossSource_ha_then_chat_within_window()
	{
		var ha = Flag(813, 0, 0);
		var chat = Flag(813, 1000, 0);
		var mem = Mem(ha, HuntFlagIntakeSource.HuntAlerts, T0);

		Assert.True(HuntAlertsFlagDedupe.ShouldSuppressCrossSource(
			mem,
			chat,
			HuntFlagIntakeSource.Chat,
			T0.AddSeconds(5),
			huntAlertsIntegration: true));
	}

	[Fact]
	public void ShouldSuppressCrossSource_false_after_window_expires()
	{
		var chat = Flag(813, 0, 0);
		var ha = Flag(813, 0, 0);
		var mem = Mem(chat, HuntFlagIntakeSource.Chat, T0);
		var after = T0 + HuntAlertsFlagDedupe.DefaultCrossSourceWindow + TimeSpan.FromMilliseconds(1);

		Assert.False(HuntAlertsFlagDedupe.ShouldSuppressCrossSource(
			mem,
			ha,
			HuntFlagIntakeSource.HuntAlerts,
			after,
			huntAlertsIntegration: true));
	}

	[Fact]
	public void ShouldSuppressCrossSource_false_for_same_source()
	{
		var prior = Flag(813, 0, 0);
		var again = Flag(813, 0, 0);
		var mem = Mem(prior, HuntFlagIntakeSource.HuntAlerts, T0);

		Assert.False(HuntAlertsFlagDedupe.ShouldSuppressCrossSource(
			mem,
			again,
			HuntFlagIntakeSource.HuntAlerts,
			T0.AddSeconds(1),
			huntAlertsIntegration: true));
	}

	[Fact]
	public void ShouldSuppressCrossSource_false_when_not_near_dup()
	{
		var chat = Flag(813, 0, 0);
		var haFar = Flag(813, 20_000, 0);
		var mem = Mem(chat, HuntFlagIntakeSource.Chat, T0);

		Assert.False(HuntAlertsFlagDedupe.ShouldSuppressCrossSource(
			mem,
			haFar,
			HuntFlagIntakeSource.HuntAlerts,
			T0.AddSeconds(1),
			huntAlertsIntegration: true));
	}

	[Fact]
	public void ShouldSuppressCrossSource_false_when_integration_disabled()
	{
		var chat = Flag(813, 0, 0);
		var ha = Flag(813, 0, 0);
		var mem = Mem(chat, HuntFlagIntakeSource.Chat, T0);

		Assert.False(HuntAlertsFlagDedupe.ShouldSuppressCrossSource(
			mem,
			ha,
			HuntFlagIntakeSource.HuntAlerts,
			T0.AddSeconds(1),
			huntAlertsIntegration: false));
	}

	[Fact]
	public void ShouldSuppressCrossSource_true_even_when_near_dup_forceAccept_would_bypass()
	{
		// Deferred flush: forceAccept strips Arrival / bypasses pipeline near-dup,
		// but cross-source window still suppresses chat-won hunts.
		var chat = Flag(813, 0, 0);
		var ha = Flag(813, 0, 0);
		var mem = Mem(chat, HuntFlagIntakeSource.Chat, T0);

		Assert.False(HuntAlertsFlagDedupe.ShouldSuppress(
			chat, ha, pipelineActive: true, forceAccept: true));
		Assert.True(HuntAlertsFlagDedupe.ShouldSuppressCrossSource(
			mem,
			ha,
			HuntFlagIntakeSource.HuntAlerts,
			T0.AddSeconds(1),
			huntAlertsIntegration: true));
	}

	[Fact]
	public void ShouldSuppressCrossSource_false_when_no_memory()
		=> Assert.False(HuntAlertsFlagDedupe.ShouldSuppressCrossSource(
			null,
			Flag(813, 0, 0),
			HuntFlagIntakeSource.HuntAlerts,
			T0,
			huntAlertsIntegration: true));

	[Fact]
	public void ShouldSuppressCrossSource_false_when_window_non_positive()
	{
		var chat = Flag(813, 0, 0);
		var ha = Flag(813, 0, 0);
		var mem = Mem(chat, HuntFlagIntakeSource.Chat, T0);

		Assert.False(HuntAlertsFlagDedupe.ShouldSuppressCrossSource(
			mem,
			ha,
			HuntFlagIntakeSource.HuntAlerts,
			T0.AddSeconds(1),
			huntAlertsIntegration: true,
			window: TimeSpan.Zero));
	}

	[Fact]
	public void Chat_adopt_remember_suppresses_deferred_ha_flush()
	{
		// Chat adopts (+ Remember) while HA had / would flush a near-dup pending.
		var chat = Flag(813, 0, 0);
		var haDeferred = Flag(813, 1500, 0);
		var afterChat = HuntAlertsFlagDedupe.Remember(
			chat, HuntFlagIntakeSource.Chat, T0);

		Assert.True(HuntAlertsFlagDedupe.ShouldSuppressCrossSource(
			afterChat,
			haDeferred,
			HuntFlagIntakeSource.HuntAlerts,
			T0.AddSeconds(5),
			huntAlertsIntegration: true));
		// forceAccept still needed so flush can strip Arrival when cross-source does not apply
		Assert.False(HuntAlertsFlagDedupe.ShouldSuppress(
			chat, haDeferred, pipelineActive: true, forceAccept: true));
	}

	[Fact]
	public void Ha_pending_then_chat_remember_blocks_later_ha_stash_and_flush()
	{
		// HA could have Stored pending earlier; chat adopt Remembers + clears pending.
		// Subsequent HA Process / flush must see chat memory and suppress.
		var ha = Flag(813, 0, 0);
		var chat = Flag(813, 500, 0);
		_ = HuntAlertsFlagDedupe.Remember(ha, HuntFlagIntakeSource.HuntAlerts, T0);
		var afterChat = HuntAlertsFlagDedupe.Remember(
			chat, HuntFlagIntakeSource.Chat, T0.AddSeconds(2));

		Assert.True(HuntAlertsFlagDedupe.ShouldSuppressCrossSource(
			afterChat,
			Flag(813, 1000, 0),
			HuntFlagIntakeSource.HuntAlerts,
			T0.AddSeconds(3),
			huntAlertsIntegration: true));
	}

	[Fact]
	public void ShouldClearPendingOnCrossSourceSuppress_true_when_no_pending()
		=> Assert.True(HuntAlertsFlagDedupe.ShouldClearPendingOnCrossSourceSuppress(
			null,
			Flag(813, 0, 0)));

	[Fact]
	public void ShouldClearPendingOnCrossSourceSuppress_true_when_pending_near_dup_of_memory()
	{
		var memory = Flag(813, 0, 0);
		var pending = Flag(813, 2000, 0);
		Assert.True(HuntAlertsFlagDedupe.ShouldClearPendingOnCrossSourceSuppress(pending, memory));
	}

	[Fact]
	public void ShouldClearPendingOnCrossSourceSuppress_false_when_pending_unrelated_to_memory()
	{
		// Pending is a different hunt (far from chat-won memory) — keep it.
		var memory = Flag(813, 0, 0);
		var pendingOtherHunt = Flag(813, 20_000, 0);
		Assert.False(HuntAlertsFlagDedupe.ShouldClearPendingOnCrossSourceSuppress(
			pendingOtherHunt,
			memory));
	}

	[Fact]
	public void ShouldClearPendingOnCrossSourceSuppress_false_when_pending_near_incoming_but_not_memory()
	{
		// Incoming HA is near-dup of memory (cross-source suppress), but pending only
		// happens to lie near the suppressed incoming — not the chat-won flag. Keep it.
		var memory = Flag(813, 0, 0);
		var suppressedIncoming = Flag(813, 9000, 0);
		var pendingNearIncoming = Flag(813, 18_000, 0);
		Assert.True(HuntAlertsFlagDedupe.IsNearDuplicate(memory, suppressedIncoming));
		Assert.True(HuntAlertsFlagDedupe.IsNearDuplicate(pendingNearIncoming, suppressedIncoming));
		Assert.False(HuntAlertsFlagDedupe.IsNearDuplicate(pendingNearIncoming, memory));
		Assert.False(HuntAlertsFlagDedupe.ShouldClearPendingOnCrossSourceSuppress(
			pendingNearIncoming,
			memory));
	}

	[Fact]
	public void ShouldClearPendingOnCrossSourceSuppress_false_when_memory_null_and_pending_exists()
		=> Assert.False(HuntAlertsFlagDedupe.ShouldClearPendingOnCrossSourceSuppress(
			Flag(813, 0, 0),
			null));

	[Fact]
	public void ShouldClearPendingOnCrossSourceSuppress_false_when_different_territory()
	{
		var pending = Flag(814, 0, 0);
		var memory = Flag(813, 0, 0);
		Assert.False(HuntAlertsFlagDedupe.ShouldClearPendingOnCrossSourceSuppress(pending, memory));
	}

	[Fact]
	public void Clear_returns_null()
	{
		var mem = Mem(Flag(813, 0, 0), HuntFlagIntakeSource.Chat, T0);
		Assert.Null(HuntAlertsFlagDedupe.Clear(mem));
		Assert.Null(HuntAlertsFlagDedupe.Clear(null));
	}

	[Fact]
	public void ShouldProceedAbortVisitThenEnter_false_when_cross_source_would_suppress()
	{
		var chat = Flag(813, 0, 0);
		var ha = Flag(813, 500, 0);
		var mem = Mem(chat, HuntFlagIntakeSource.Chat, T0);

		Assert.False(HuntAlertsFlagDedupe.ShouldProceedAbortVisitThenEnter(
			activeFlag: null,
			ha,
			pipelineActive: false,
			crossSourceMemory: mem,
			now: T0.AddSeconds(2),
			huntAlertsIntegration: true));
	}

	[Fact]
	public void Remember_stores_flag_source_and_time()
	{
		var flag = Flag(813, 1, 2);
		var mem = HuntAlertsFlagDedupe.Remember(flag, HuntFlagIntakeSource.Chat, T0);

		Assert.Same(flag, mem.Flag);
		Assert.Equal(HuntFlagIntakeSource.Chat, mem.Source);
		Assert.Equal(T0, mem.AcceptedAt);
	}
}

/// <summary>
/// TASKS 10.7 coverage: message → HuntFlag / aetheryte; HA↔chat window dedup; disabled no-op.
/// </summary>
public sealed class HuntAlertsCrossSourceDedupeSuiteTests
{
	private static HuntTrainMessage ValidMessage(
		uint aetheryteId = 42,
		float mapX = 12.3f,
		float mapY = 24.5f)
		=> new()
		{
			huntType = HuntAlertsFilter.HuntTypeATrain,
			huntWorld = "Phoenix",
			startTerritoryTypeId = 813,
			startLocationAetheryteId = aetheryteId,
			startLocation = "Fort Jobb",
			startZone = "Lakeland",
			mapLocationX = mapX,
			mapLocationY = mapY,
			instance = 2,
		};

	[Fact]
	public void Message_maps_to_HuntFlag_with_aetheryte_arrival()
	{
		Assert.True(HuntTrainMessageMapper.TryMap(
			ValidMessage(),
			huntAlertsIntegration: true,
			rankFilter: null,
			worldBlacklist: null,
			out var flag,
			mapId: 456,
			sizeFactor: 100f,
			timestamp: DateTimeOffset.UnixEpoch));

		Assert.Equal(813u, flag.TerritoryTypeId);
		Assert.Equal(456u, flag.MapId);
		Assert.Equal("Phoenix", flag.HuntWorld);
		Assert.NotNull(flag.Arrival);
		Assert.Equal(42u, flag.Arrival!.AetheryteId);
		Assert.Equal(813u, flag.Arrival.Territory);
		Assert.Equal(2, flag.Arrival.Instance);
		Assert.Equal("Phoenix", flag.Arrival.World);

		var expectedX = MapCoordinates.ConvertMapCoordinateToRawPosition(
			12.3f + HuntTrainMessageMapper.DefaultMapCoordFudge, 100f);
		var expectedY = MapCoordinates.ConvertMapCoordinateToRawPosition(
			24.5f + HuntTrainMessageMapper.DefaultMapCoordFudge, 100f);
		Assert.Equal(expectedX, flag.RawX);
		Assert.Equal(expectedY, flag.RawY);
	}

	[Fact]
	public void Mapped_flag_dedupes_against_chat_within_window()
	{
		Assert.True(HuntTrainMessageMapper.TryMap(
			ValidMessage(),
			huntAlertsIntegration: true,
			rankFilter: null,
			worldBlacklist: null,
			out var haFlag,
			sizeFactor: 100f,
			timestamp: DateTimeOffset.UnixEpoch));

		var chat = HuntFlag.FromMapLink(
			haFlag.TerritoryTypeId,
			haFlag.MapId,
			haFlag.RawX,
			haFlag.RawY,
			haFlag.PlaceName,
			DateTimeOffset.UnixEpoch);
		var mem = HuntAlertsFlagDedupe.Remember(
			chat,
			HuntFlagIntakeSource.Chat,
			DateTimeOffset.UnixEpoch);

		Assert.True(HuntAlertsFlagDedupe.ShouldSuppressCrossSource(
			mem,
			haFlag,
			HuntFlagIntakeSource.HuntAlerts,
			DateTimeOffset.UnixEpoch.AddSeconds(15),
			huntAlertsIntegration: true));
	}

	[Fact]
	public void Integration_disabled_is_noop_for_map_and_cross_source_dedupe()
	{
		Assert.False(HuntTrainMessageMapper.TryMap(
			ValidMessage(),
			huntAlertsIntegration: false,
			rankFilter: null,
			worldBlacklist: null,
			out _));

		var chat = HuntFlag.FromMapLink(813, 1, 0, 0, "x", DateTimeOffset.UnixEpoch);
		var ha = HuntFlag.FromMapLink(813, 1, 0, 0, "x", DateTimeOffset.UnixEpoch);
		var mem = HuntAlertsFlagDedupe.Remember(
			chat,
			HuntFlagIntakeSource.Chat,
			DateTimeOffset.UnixEpoch);

		Assert.False(HuntAlertsFlagDedupe.ShouldSuppressCrossSource(
			mem,
			ha,
			HuntFlagIntakeSource.HuntAlerts,
			DateTimeOffset.UnixEpoch.AddSeconds(1),
			huntAlertsIntegration: false));
	}
}
