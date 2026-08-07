#nullable enable

using System;
using HuntTrainAuto.Contracts;
using HuntTrainAuto.Domain;
using HuntTrainAuto.HuntAlerts;

namespace HuntTrainAuto.Tests.HuntAlerts;

public sealed class HuntAlertsWorldVisitTests
{
	[Fact]
	public void TryHandle_uses_HuntWorld_when_Arrival_absent()
	{
		var flag = HuntFlag.FromMapLink(813, 1, 0, 0, "x", DateTimeOffset.UnixEpoch);
		flag.HuntWorld = "Cerberus";
		Assert.Null(flag.Arrival);

		var life = new StubLifestream
		{
			Available = true,
			Busy = false,
			CanSame = true,
			ChangeWorldResult = true,
		};

		var result = HuntAlertsWorldVisit.TryHandle(
			flag,
			huntAlertsIntegration: true,
			life,
			currentWorldName: "Phoenix");

		Assert.Equal(HuntAlertsWorldVisitAction.RequestWorldVisit, result.Action);
		Assert.Equal("Cerberus", result.World);
		Assert.Equal(1, life.ChangeWorldCalls);
		Assert.Equal("Cerberus", life.LastChangeWorld);
	}

	[Fact]
	public void TryHandle_falls_back_to_Arrival_World()
	{
		var flag = HuntFlag.FromMapLink(813, 1, 0, 0, "x", DateTimeOffset.UnixEpoch);
		flag.Arrival = ArrivalData.CreateOrNull(10u, 813u, 1, "Gilgamesh");

		var life = new StubLifestream
		{
			Available = true,
			Busy = false,
			CanCross = true,
			ChangeWorldResult = true,
		};

		var result = HuntAlertsWorldVisit.TryHandle(
			flag,
			huntAlertsIntegration: true,
			life,
			currentWorldName: "Phoenix");

		Assert.Equal(HuntAlertsWorldVisitAction.RequestWorldVisit, result.Action);
		Assert.Equal("Gilgamesh", result.World);
		Assert.Equal(1, life.ChangeWorldCalls);
	}

	[Fact]
	public void TryHandle_ChangeWorld_false_returns_NoOp()
	{
		var flag = HuntFlag.FromMapLink(813, 1, 0, 0, "x", DateTimeOffset.UnixEpoch);
		flag.HuntWorld = "Cerberus";

		var life = new StubLifestream
		{
			Available = true,
			Busy = false,
			CanSame = true,
			ChangeWorldResult = false,
		};

		var result = HuntAlertsWorldVisit.TryHandle(
			flag,
			huntAlertsIntegration: true,
			life,
			currentWorldName: "Phoenix");

		Assert.Equal(HuntAlertsWorldVisitAction.NoOp, result.Action);
		Assert.Null(result.World);
		Assert.True(result.AttemptedChangeWorld);
		Assert.Equal(1, life.ChangeWorldCalls);
	}

	[Fact]
	public void TryHandle_unknown_current_world_skips_ChangeWorld()
	{
		var flag = HuntFlag.FromMapLink(813, 1, 0, 0, "x", DateTimeOffset.UnixEpoch);
		flag.HuntWorld = "Cerberus";

		var life = new StubLifestream
		{
			Available = true,
			Busy = false,
			CanSame = true,
			ChangeWorldResult = true,
		};

		var result = HuntAlertsWorldVisit.TryHandle(
			flag,
			huntAlertsIntegration: true,
			life,
			currentWorldName: null);

		Assert.Equal(HuntAlertsWorldVisitAction.UnknownCurrentWorld, result.Action);
		Assert.False(result.AttemptedChangeWorld);
		Assert.Equal(0, life.ChangeWorldCalls);
	}

	[Fact]
	public void TryHandle_busy_returns_BusyMidVisit_without_ChangeWorld()
	{
		var flag = HuntFlag.FromMapLink(813, 1, 0, 0, "x", DateTimeOffset.UnixEpoch);
		flag.HuntWorld = "Cerberus";

		var life = new StubLifestream
		{
			Available = true,
			Busy = true,
			CanSame = true,
			ChangeWorldResult = true,
		};

		var result = HuntAlertsWorldVisit.TryHandle(
			flag,
			huntAlertsIntegration: true,
			life,
			currentWorldName: "Phoenix");

		Assert.Equal(HuntAlertsWorldVisitAction.BusyMidVisit, result.Action);
		Assert.Equal("Cerberus", result.World);
		Assert.False(result.AttemptedChangeWorld);
		Assert.Equal(0, life.ChangeWorldCalls);
		Assert.Equal(0, life.AbortCalls);
	}

	[Fact]
	public void TryHandle_busy_unknown_current_returns_BusyMidVisit_not_Unknown()
	{
		var flag = HuntFlag.FromMapLink(813, 1, 0, 0, "x", DateTimeOffset.UnixEpoch);
		flag.HuntWorld = "Cerberus";

		var life = new StubLifestream
		{
			Available = true,
			Busy = true,
			CanSame = true,
			ChangeWorldResult = true,
		};

		var result = HuntAlertsWorldVisit.TryHandle(
			flag,
			huntAlertsIntegration: true,
			life,
			currentWorldName: null,
			hasPendingDefer: true,
			pendingDeferWorld: "Phoenix");

		Assert.Equal(HuntAlertsWorldVisitAction.BusyMidVisit, result.Action);
		Assert.Equal("Cerberus", result.World);
		Assert.Equal(0, life.ChangeWorldCalls);
		Assert.Equal(0, life.AbortCalls);
	}

	[Fact]
	public void TryHandle_different_world_defer_replace_Aborts_before_ChangeWorld()
	{
		var flag = HuntFlag.FromMapLink(813, 1, 0, 0, "x", DateTimeOffset.UnixEpoch);
		flag.HuntWorld = "Phoenix";

		var life = new StubLifestream
		{
			Available = true,
			Busy = false,
			CanSame = true,
			ChangeWorldResult = true,
		};

		var result = HuntAlertsWorldVisit.TryHandle(
			flag,
			huntAlertsIntegration: true,
			life,
			currentWorldName: "Gilgamesh",
			hasPendingDefer: true,
			pendingDeferWorld: "Cerberus");

		Assert.Equal(HuntAlertsWorldVisitAction.RequestWorldVisit, result.Action);
		Assert.Equal("Phoenix", result.World);
		Assert.Equal(1, life.AbortCalls);
		Assert.Equal(1, life.ChangeWorldCalls);
		Assert.True(life.AbortBeforeChangeWorld);
	}

	[Fact]
	public void TryHandle_same_world_defer_replace_skips_Abort()
	{
		var flag = HuntFlag.FromMapLink(813, 1, 0, 0, "x", DateTimeOffset.UnixEpoch);
		flag.HuntWorld = "Cerberus";

		var life = new StubLifestream
		{
			Available = true,
			Busy = false,
			CanSame = true,
			ChangeWorldResult = true,
		};

		var result = HuntAlertsWorldVisit.TryHandle(
			flag,
			huntAlertsIntegration: true,
			life,
			currentWorldName: "Phoenix",
			hasPendingDefer: true,
			pendingDeferWorld: "cerberus");

		Assert.Equal(HuntAlertsWorldVisitAction.RequestWorldVisit, result.Action);
		Assert.Equal(0, life.AbortCalls);
		Assert.Equal(1, life.ChangeWorldCalls);
	}

	[Fact]
	public void TryHandle_no_pending_skips_Abort()
	{
		var flag = HuntFlag.FromMapLink(813, 1, 0, 0, "x", DateTimeOffset.UnixEpoch);
		flag.HuntWorld = "Cerberus";

		var life = new StubLifestream
		{
			Available = true,
			Busy = false,
			CanSame = true,
			ChangeWorldResult = true,
		};

		_ = HuntAlertsWorldVisit.TryHandle(
			flag,
			huntAlertsIntegration: true,
			life,
			currentWorldName: "Phoenix");

		Assert.Equal(0, life.AbortCalls);
		Assert.Equal(1, life.ChangeWorldCalls);
	}

	[Fact]
	public void TryHandle_different_world_defer_replace_ChangeWorld_false_returns_DeferReplaceFailed_with_world()
	{
		var flag = HuntFlag.FromMapLink(813, 1, 0, 0, "x", DateTimeOffset.UnixEpoch);
		flag.HuntWorld = "Phoenix";

		var life = new StubLifestream
		{
			Available = true,
			Busy = false,
			CanSame = true,
			ChangeWorldResult = false,
		};

		var result = HuntAlertsWorldVisit.TryHandle(
			flag,
			huntAlertsIntegration: true,
			life,
			currentWorldName: "Gilgamesh",
			hasPendingDefer: true,
			pendingDeferWorld: "Cerberus");

		Assert.Equal(HuntAlertsWorldVisitAction.DeferReplaceFailed, result.Action);
		Assert.Equal("Phoenix", result.World);
		Assert.True(result.AttemptedChangeWorld);
		Assert.Equal(1, life.AbortCalls);
		// First ChangeWorld fail + not-busy retry.
		Assert.Equal(2, life.ChangeWorldCalls);
		Assert.True(life.AbortBeforeChangeWorld);
	}

	[Fact]
	public void TryHandle_defer_replace_ChangeWorld_retry_succeeds_returns_RequestWorldVisit()
	{
		var flag = HuntFlag.FromMapLink(813, 1, 0, 0, "x", DateTimeOffset.UnixEpoch);
		flag.HuntWorld = "Phoenix";

		var life = new StubLifestream
		{
			Available = true,
			Busy = false,
			CanSame = true,
			ChangeWorldResults = [false, true],
		};

		var result = HuntAlertsWorldVisit.TryHandle(
			flag,
			huntAlertsIntegration: true,
			life,
			currentWorldName: "Gilgamesh",
			hasPendingDefer: true,
			pendingDeferWorld: "Cerberus");

		Assert.Equal(HuntAlertsWorldVisitAction.RequestWorldVisit, result.Action);
		Assert.Equal("Phoenix", result.World);
		Assert.Equal(1, life.AbortCalls);
		Assert.Equal(2, life.ChangeWorldCalls);
	}

	[Fact]
	public void TryHandle_defer_replace_still_busy_after_Abort_skips_retry()
	{
		var flag = HuntFlag.FromMapLink(813, 1, 0, 0, "x", DateTimeOffset.UnixEpoch);
		flag.HuntWorld = "Phoenix";

		// Decide samples Busy=false → RequestWorldVisit; after Abort IsBusy stays true → no retry.
		var life = new StubLifestream
		{
			Available = true,
			Busy = false,
			BusyAfterAbort = true,
			CanSame = true,
			ChangeWorldResult = false,
		};

		var result = HuntAlertsWorldVisit.TryHandle(
			flag,
			huntAlertsIntegration: true,
			life,
			currentWorldName: "Gilgamesh",
			hasPendingDefer: true,
			pendingDeferWorld: "Cerberus");

		Assert.Equal(HuntAlertsWorldVisitAction.DeferReplaceFailed, result.Action);
		Assert.Equal("Phoenix", result.World);
		Assert.Equal(1, life.AbortCalls);
		Assert.Equal(1, life.ChangeWorldCalls);
	}

	[Fact]
	public void TryHandle_same_world_pending_ChangeWorld_false_returns_BusyMidVisit_with_world()
	{
		// Same pending world + failed ChangeWorld (no Abort): refresh path — World set so
		// intake RefreshFlagKeepWorld updates the flag (NoOp+null would Skip / keep older).
		var flag = HuntFlag.FromMapLink(813, 1, 0, 0, "x", DateTimeOffset.UnixEpoch);
		flag.HuntWorld = "Cerberus";

		var life = new StubLifestream
		{
			Available = true,
			Busy = false,
			CanSame = true,
			ChangeWorldResult = false,
		};

		var result = HuntAlertsWorldVisit.TryHandle(
			flag,
			huntAlertsIntegration: true,
			life,
			currentWorldName: "Phoenix",
			hasPendingDefer: true,
			pendingDeferWorld: "Cerberus");

		Assert.Equal(HuntAlertsWorldVisitAction.BusyMidVisit, result.Action);
		Assert.Equal("Cerberus", result.World);
		Assert.True(result.AttemptedChangeWorld);
		Assert.Equal(0, life.AbortCalls);
		Assert.Equal(1, life.ChangeWorldCalls);
	}

	[Fact]
	public void TryHandle_RequestWorldVisit_success_sets_AttemptedChangeWorld()
	{
		var flag = HuntFlag.FromMapLink(813, 1, 0, 0, "x", DateTimeOffset.UnixEpoch);
		flag.HuntWorld = "Cerberus";

		var life = new StubLifestream
		{
			Available = true,
			Busy = false,
			CanSame = true,
			ChangeWorldResult = true,
		};

		var result = HuntAlertsWorldVisit.TryHandle(
			flag,
			huntAlertsIntegration: true,
			life,
			currentWorldName: "Phoenix");

		Assert.Equal(HuntAlertsWorldVisitAction.RequestWorldVisit, result.Action);
		Assert.True(result.AttemptedChangeWorld);
		Assert.Equal(1, life.ChangeWorldCalls);
	}

	private sealed class StubLifestream : ILifestreamService
	{
		public bool Available { get; init; }
		public bool Busy { get; init; }
		/// <summary>When set, <see cref="IsBusy"/> returns this after the first <see cref="Abort"/>.</summary>
		public bool? BusyAfterAbort { get; init; }
		public bool CanSame { get; init; }
		public bool CanCross { get; init; }
		public bool ChangeWorldResult { get; init; } = true;
		/// <summary>Per-call results; falls back to <see cref="ChangeWorldResult"/> when exhausted.</summary>
		public bool[]? ChangeWorldResults { get; init; }

		public int ChangeWorldCalls { get; private set; }
		public int AbortCalls { get; private set; }
		public string? LastChangeWorld { get; private set; }
		public bool AbortBeforeChangeWorld { get; private set; }

		public bool IsAvailable => Available;

		public bool Teleport(uint aetheryteId, byte subIndex = 0) => false;

		public void ChangeInstance(int instance) { }

		public int GetCurrentInstance() => 0;

		public int GetNumberOfInstances() => 0;

		public bool CanChangeInstance() => false;

		public bool IsBusy()
		{
			if (AbortCalls > 0 && BusyAfterAbort.HasValue)
				return BusyAfterAbort.Value;
			return Busy;
		}

		public void Abort()
		{
			AbortCalls++;
			if (ChangeWorldCalls == 0)
				AbortBeforeChangeWorld = true;
		}

		public bool CanVisitSameDC(string world) => CanSame;

		public bool CanVisitCrossDC(string world) => CanCross;

		public bool ChangeWorld(string world)
		{
			var call = ChangeWorldCalls++;
			LastChangeWorld = world;
			if (ChangeWorldResults != null && call < ChangeWorldResults.Length)
				return ChangeWorldResults[call];
			return ChangeWorldResult;
		}

		public void Dispose() { }
	}
}
