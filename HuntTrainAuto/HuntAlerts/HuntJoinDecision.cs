#nullable enable

using System;

namespace HuntTrainAuto.HuntAlerts;

/// <summary>Framework phases for the go-to-hunt + find-conductor button.</summary>
public enum HuntJoinPhase
{
	Idle,
	WorldVisit,
	Teleport,
	WaitInstance,
	SearchAssign,
}

/// <summary>Pure next action for <see cref="HuntJoinPhase"/>.</summary>
public enum HuntJoinStep
{
	Wait,
	ChangeWorld,
	Teleport,
	EnqueueInstance,
	SearchAndAssign,
	TimedOut,
	AdvanceToTeleport,
	AdvanceToInstance,
	AdvanceToSearch,
}

/// <summary>
/// Pure plan / tick decisions for joining a HuntAlerts train: world visit,
/// aetheryte TP, then <c>/sea first</c> + conductor assign after arrival
/// (so LeaveHuntingFull during the hop cannot wipe the name).
/// </summary>
public static class HuntJoinDecision
{
	public const int OverallTimeoutMs = 180_000;
	public const int RetryIntervalMs = 3_000;

	public readonly record struct Plan(
		string World,
		uint TerritoryTypeId,
		uint AetheryteId,
		string PlaceName,
		string ConductorName,
		int Instance);

	public static string FormatSearchCommand(string conductorName)
		=> $"/sea first {conductorName.Trim()}";

	public static string Describe(Plan plan)
		=> $"{plan.World} / {plan.PlaceName} → {plan.ConductorName}";

	public static bool TryPlan(HuntTrainMessage? message, out Plan plan, out string rejectReason)
	{
		plan = default;
		rejectReason = "no HuntAlerts train";
		if (message == null)
			return false;

		var world = message.huntWorld?.Trim() ?? "";
		if (string.IsNullOrEmpty(world)
		    || world.Equals("invalid", StringComparison.OrdinalIgnoreCase)
		    || world.Equals("unknown", StringComparison.OrdinalIgnoreCase))
		{
			rejectReason = "missing hunt world";
			return false;
		}

		if (message.startLocationAetheryteId == 0)
		{
			rejectReason = "missing start aetheryte";
			return false;
		}

		if (message.startTerritoryTypeId == 0)
		{
			rejectReason = "missing start territory";
			return false;
		}

		if (!HuntAlertsConductorParse.TryExtract(message.Message, out var name, out _))
		{
			rejectReason = "no conductor name";
			return false;
		}

		var place = FirstNonEmpty(message.startLocation, message.startZone) ?? "start";
		plan = new Plan(
			world,
			message.startTerritoryTypeId,
			message.startLocationAetheryteId,
			place,
			name,
			message.instance);
		rejectReason = "";
		return true;
	}

	public static bool IsWorldMatch(string? currentWorld, string targetWorld)
	{
		if (string.IsNullOrWhiteSpace(currentWorld) || string.IsNullOrWhiteSpace(targetWorld))
			return false;
		return string.Equals(currentWorld.Trim(), targetWorld.Trim(), StringComparison.OrdinalIgnoreCase);
	}

	public static bool IsTerritoryMatch(uint currentTerritory, uint targetTerritory)
		=> targetTerritory != 0 && currentTerritory == targetTerritory;

	public static bool IsLanded(bool playerReady, bool betweenAreas)
		=> playerReady && !betweenAreas;

	public static HuntJoinPhase InitialPhase(
		bool worldMatches,
		bool territoryMatches,
		bool landed,
		bool needsInstance)
	{
		if (worldMatches && territoryMatches && landed)
			return needsInstance ? HuntJoinPhase.WaitInstance : HuntJoinPhase.SearchAssign;
		if (worldMatches)
			return HuntJoinPhase.Teleport;
		return HuntJoinPhase.WorldVisit;
	}

	public static HuntJoinStep Decide(
		HuntJoinPhase phase,
		bool worldMatches,
		bool territoryMatches,
		bool betweenAreas,
		bool playerReady,
		bool lifestreamBusy,
		bool instanceJobActive,
		bool needsInstance,
		bool retryReady,
		bool timedOut)
	{
		if (timedOut)
			return HuntJoinStep.TimedOut;

		var landed = IsLanded(playerReady, betweenAreas);

		switch (phase)
		{
			case HuntJoinPhase.WorldVisit:
				if (worldMatches && territoryMatches && landed)
					return needsInstance ? HuntJoinStep.AdvanceToInstance : HuntJoinStep.AdvanceToSearch;
				if (worldMatches && landed)
					return HuntJoinStep.AdvanceToTeleport;
				if (lifestreamBusy || betweenAreas || !retryReady)
					return HuntJoinStep.Wait;
				return HuntJoinStep.ChangeWorld;

			case HuntJoinPhase.Teleport:
				if (!worldMatches)
					return HuntJoinStep.ChangeWorld;
				if (territoryMatches && landed)
					return needsInstance ? HuntJoinStep.AdvanceToInstance : HuntJoinStep.AdvanceToSearch;
				if (betweenAreas || !playerReady || !retryReady)
					return HuntJoinStep.Wait;
				return HuntJoinStep.Teleport;

			case HuntJoinPhase.WaitInstance:
				return instanceJobActive ? HuntJoinStep.Wait : HuntJoinStep.AdvanceToSearch;

			case HuntJoinPhase.SearchAssign:
				return HuntJoinStep.SearchAndAssign;

			default:
				return HuntJoinStep.Wait;
		}
	}

	private static string? FirstNonEmpty(string a, string b)
	{
		var x = a?.Trim();
		if (!string.IsNullOrEmpty(x))
			return x;
		var y = b?.Trim();
		return string.IsNullOrEmpty(y) ? null : y;
	}
}
