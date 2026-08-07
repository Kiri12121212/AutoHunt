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
		Assert.Equal(0, life.ChangeWorldCalls);
	}

	private sealed class StubLifestream : ILifestreamService
	{
		public bool Available { get; init; }
		public bool Busy { get; init; }
		public bool CanSame { get; init; }
		public bool CanCross { get; init; }
		public bool ChangeWorldResult { get; init; } = true;

		public int ChangeWorldCalls { get; private set; }
		public string? LastChangeWorld { get; private set; }

		public bool IsAvailable => Available;

		public bool Teleport(uint aetheryteId, byte subIndex = 0) => false;

		public void ChangeInstance(int instance) { }

		public int GetCurrentInstance() => 0;

		public int GetNumberOfInstances() => 0;

		public bool CanChangeInstance() => false;

		public bool IsBusy() => Busy;

		public bool CanVisitSameDC(string world) => CanSame;

		public bool CanVisitCrossDC(string world) => CanCross;

		public bool ChangeWorld(string world)
		{
			ChangeWorldCalls++;
			LastChangeWorld = world;
			return ChangeWorldResult;
		}

		public void Dispose() { }
	}
}
