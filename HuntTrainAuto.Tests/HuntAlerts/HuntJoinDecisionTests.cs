#nullable enable

namespace HuntTrainAuto.Tests.HuntAlerts;

public sealed class HuntJoinDecisionTests
{
	private static HuntTrainMessage LichTrain()
		=> new()
		{
			huntType = HuntAlertsFilter.HuntTypeATrain,
			huntKind = "Dawntrail",
			huntWorld = "Lich",
			startLocation = "Electrope Strike",
			startLocationAetheryteId = 212,
			startZone = "Heritage Found",
			startTerritoryTypeId = 1191,
			instance = 1,
			Message =
				"**[Lich]** Hunt train starting 04:58 PM at  **Electrope Strike, Heritage Found - Instance 1 **| FAST (Conductor: Sassy Kitten [Lich]).",
		};

	[Fact]
	public void TryPlan_live_lich_ha_message()
	{
		Assert.True(HuntJoinDecision.TryPlan(LichTrain(), out var plan, out var reject));
		Assert.Equal("", reject);
		Assert.Equal("Lich", plan.World);
		Assert.Equal(1191u, plan.TerritoryTypeId);
		Assert.Equal(212u, plan.AetheryteId);
		Assert.Equal("Electrope Strike", plan.PlaceName);
		Assert.Equal("Sassy Kitten", plan.ConductorName);
		Assert.Equal(1, plan.Instance);
		Assert.Equal("/sea first Sassy Kitten", HuntJoinDecision.FormatSearchCommand(plan.ConductorName));
		Assert.Equal("Lich / Electrope Strike → Sassy Kitten", HuntJoinDecision.Describe(plan));
	}

	[Fact]
	public void TryPlan_rejects_null_and_incomplete()
	{
		Assert.False(HuntJoinDecision.TryPlan(null, out _, out var noAlert));
		Assert.Equal("no HuntAlerts train", noAlert);

		var noWorld = LichTrain();
		noWorld.huntWorld = "";
		Assert.False(HuntJoinDecision.TryPlan(noWorld, out _, out var missingWorld));
		Assert.Equal("missing hunt world", missingWorld);

		var noAeth = LichTrain();
		noAeth.startLocationAetheryteId = 0;
		Assert.False(HuntJoinDecision.TryPlan(noAeth, out _, out var missingAeth));
		Assert.Equal("missing start aetheryte", missingAeth);

		var noTerr = LichTrain();
		noTerr.startTerritoryTypeId = 0;
		Assert.False(HuntJoinDecision.TryPlan(noTerr, out _, out var missingTerr));
		Assert.Equal("missing start territory", missingTerr);

		var noCond = LichTrain();
		noCond.Message = "Hunt train starting soon";
		Assert.False(HuntJoinDecision.TryPlan(noCond, out _, out var missingCond));
		Assert.Equal("no conductor name", missingCond);
	}

	[Fact]
	public void IsWorldMatch_is_case_insensitive()
	{
		Assert.True(HuntJoinDecision.IsWorldMatch("lich", "Lich"));
		Assert.False(HuntJoinDecision.IsWorldMatch(null, "Lich"));
		Assert.False(HuntJoinDecision.IsWorldMatch("Phoenix", "Lich"));
	}

	[Fact]
	public void InitialPhase_prefers_search_when_already_there()
	{
		Assert.Equal(
			HuntJoinPhase.SearchAssign,
			HuntJoinDecision.InitialPhase(true, true, true, needsInstance: false));
		Assert.Equal(
			HuntJoinPhase.WaitInstance,
			HuntJoinDecision.InitialPhase(true, true, true, needsInstance: true));
		Assert.Equal(
			HuntJoinPhase.Teleport,
			HuntJoinDecision.InitialPhase(true, false, true, needsInstance: false));
		Assert.Equal(
			HuntJoinPhase.WorldVisit,
			HuntJoinDecision.InitialPhase(false, false, true, needsInstance: false));
	}

	[Fact]
	public void Decide_world_visit_issues_then_advances()
	{
		Assert.Equal(
			HuntJoinStep.ChangeWorld,
			HuntJoinDecision.Decide(
				HuntJoinPhase.WorldVisit,
				worldMatches: false,
				territoryMatches: false,
				betweenAreas: false,
				playerReady: true,
				lifestreamBusy: false,
				instanceJobActive: false,
				needsInstance: false,
				retryReady: true,
				timedOut: false));
		Assert.Equal(
			HuntJoinStep.Wait,
			HuntJoinDecision.Decide(
				HuntJoinPhase.WorldVisit,
				worldMatches: false,
				territoryMatches: false,
				betweenAreas: false,
				playerReady: true,
				lifestreamBusy: true,
				instanceJobActive: false,
				needsInstance: false,
				retryReady: true,
				timedOut: false));
		Assert.Equal(
			HuntJoinStep.AdvanceToTeleport,
			HuntJoinDecision.Decide(
				HuntJoinPhase.WorldVisit,
				worldMatches: true,
				territoryMatches: false,
				betweenAreas: false,
				playerReady: true,
				lifestreamBusy: false,
				instanceJobActive: false,
				needsInstance: false,
				retryReady: true,
				timedOut: false));
	}

	[Fact]
	public void Decide_teleport_then_search_after_land()
	{
		Assert.Equal(
			HuntJoinStep.Teleport,
			HuntJoinDecision.Decide(
				HuntJoinPhase.Teleport,
				worldMatches: true,
				territoryMatches: false,
				betweenAreas: false,
				playerReady: true,
				lifestreamBusy: false,
				instanceJobActive: false,
				needsInstance: false,
				retryReady: true,
				timedOut: false));
		Assert.Equal(
			HuntJoinStep.AdvanceToSearch,
			HuntJoinDecision.Decide(
				HuntJoinPhase.Teleport,
				worldMatches: true,
				territoryMatches: true,
				betweenAreas: false,
				playerReady: true,
				lifestreamBusy: false,
				instanceJobActive: false,
				needsInstance: false,
				retryReady: true,
				timedOut: false));
		Assert.Equal(
			HuntJoinStep.AdvanceToInstance,
			HuntJoinDecision.Decide(
				HuntJoinPhase.Teleport,
				worldMatches: true,
				territoryMatches: true,
				betweenAreas: false,
				playerReady: true,
				lifestreamBusy: false,
				instanceJobActive: false,
				needsInstance: true,
				retryReady: true,
				timedOut: false));
	}

	[Fact]
	public void Decide_timeout_and_search()
	{
		Assert.Equal(
			HuntJoinStep.TimedOut,
			HuntJoinDecision.Decide(
				HuntJoinPhase.WorldVisit,
				worldMatches: false,
				territoryMatches: false,
				betweenAreas: false,
				playerReady: true,
				lifestreamBusy: false,
				instanceJobActive: false,
				needsInstance: false,
				retryReady: true,
				timedOut: true));
		Assert.Equal(
			HuntJoinStep.SearchAndAssign,
			HuntJoinDecision.Decide(
				HuntJoinPhase.SearchAssign,
				worldMatches: true,
				territoryMatches: true,
				betweenAreas: false,
				playerReady: true,
				lifestreamBusy: false,
				instanceJobActive: false,
				needsInstance: false,
				retryReady: true,
				timedOut: false));
		Assert.Equal(
			HuntJoinStep.AdvanceToSearch,
			HuntJoinDecision.Decide(
				HuntJoinPhase.WaitInstance,
				worldMatches: true,
				territoryMatches: true,
				betweenAreas: false,
				playerReady: true,
				lifestreamBusy: false,
				instanceJobActive: false,
				needsInstance: true,
				retryReady: true,
				timedOut: false));
	}
}
