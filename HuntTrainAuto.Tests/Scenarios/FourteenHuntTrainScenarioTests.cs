#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace HuntTrainAuto.Tests.Scenarios;

/// <summary>
/// Layer A regression: fourteen synthetic hunt legs through TP / path / engage / combat / RSR.
/// Fails when phase or decision contracts regress — not a live-game integration test.
/// </summary>
public sealed class FourteenHuntTrainScenarioTests
{
	[Fact]
	public void Fourteen_hunt_train_exercises_tp_nav_engage_combat_contracts()
	{
		var d = new TrainScenarioDriver
		{
			Territory = Zones.Labyrinthos,
			Instance = 1,
			Position = new Vector3(100f, 0f, 100f),
		};

		var results = new List<LegResult>();

		// 1. Cross-zone TP + fly + NearbyARank
		results.Add(d.RunLeg(new HuntLegSpec
		{
			Name = "01-cross-zone-fly",
			Territory = Zones.Thavnair,
			FlagWorld = new Vector3(500f, 10f, 500f),
			AetheryteWorld = new Vector3(480f, 10f, 480f),
			AetheryteId = 2001,
			FlyZone = true,
			Engage = EngageScriptKind.NearbyARank,
			SimulateMeshReadyThenPath = true,
			ExpectedTeleport = TeleportAction.TeleportToZone,
			ExpectedEngage = EngageTargetKind.NearbyARank,
			ExpectedRestart = FlagRestartKind.StartFromIdle,
		}));

		// 2. Same-zone close — skip TP
		results.Add(d.RunLeg(new HuntLegSpec
		{
			Name = "02-same-zone-close",
			Territory = Zones.Thavnair,
			FlagWorld = d.Position + new Vector3(40f, 0f, 0f),
			AetheryteWorld = new Vector3(480f, 10f, 480f),
			FlyZone = true,
			Engage = EngageScriptKind.NearbyARank,
			ExpectedTeleport = TeleportAction.Skip,
			ExpectedEngage = EngageTargetKind.NearbyARank,
		}));

		// 3. Same-zone far — TeleportBecauseFar
		results.Add(d.RunLeg(new HuntLegSpec
		{
			Name = "03-same-zone-far",
			Territory = Zones.Thavnair,
			FlagWorld = d.Position + new Vector3(400f, 0f, 0f),
			AetheryteWorld = d.Position + new Vector3(390f, 0f, 0f),
			AetheryteId = 2001,
			FlyZone = true,
			Engage = EngageScriptKind.NearbyARank,
			ExpectedTeleport = TeleportAction.TeleportBecauseFar,
			ExpectedEngage = EngageTargetKind.NearbyARank,
		}));

		// 4. Instance switch (same territory, far — close distance skips instance per TeleportDecision)
		results.Add(d.RunLeg(new HuntLegSpec
		{
			Name = "04-instance-switch",
			Territory = Zones.Thavnair,
			FlagWorld = d.Position + new Vector3(400f, 0f, 0f),
			AetheryteWorld = d.Position + new Vector3(390f, 0f, 0f),
			AetheryteId = 2001,
			TargetInstance = 2,
			FlyZone = true,
			Engage = EngageScriptKind.NearbyARank,
			ExpectedTeleport = TeleportAction.SwitchInstance,
			ExpectedEngage = EngageTargetKind.NearbyARank,
		}));
		Assert.Equal(2, d.Instance);

		// 5. Already mounted cross-zone — StartTeleport (skip mount-before-TP)
		d.Mounted = true;
		d.InFlight = true;
		results.Add(d.RunLeg(new HuntLegSpec
		{
			Name = "05-mounted-cross-zone",
			Territory = Zones.Garlemald,
			FlagWorld = new Vector3(200f, 5f, 200f),
			AetheryteWorld = new Vector3(190f, 5f, 190f),
			AetheryteId = 3001,
			FlyZone = true,
			Engage = EngageScriptKind.NearbyARank,
			ExpectedTeleport = TeleportAction.TeleportToZone,
			ExpectedEngage = EngageTargetKind.NearbyARank,
		}));
		Assert.Equal(HuntTrainEvent.StartTeleport, results[^1].Restart.StartEvent);

		// 6. Divert mid-navigate to NearbyARank
		results.Add(d.RunLeg(new HuntLegSpec
		{
			Name = "06-divert-mid-nav",
			Territory = Zones.Garlemald,
			FlagWorld = d.Position + new Vector3(80f, 0f, 0f),
			FlyZone = true,
			Engage = EngageScriptKind.DivertNearbyARank,
			DivertMidNavigate = true,
			ExpectedTeleport = TeleportAction.Skip,
			ExpectedEngage = EngageTargetKind.NearbyARank,
		}));
		Assert.Contains(HuntTrainPhase.Combat, results[^1].Phases);
		Assert.DoesNotContain(HuntTrainPhase.Unmount, results[^1].Phases);

		// 7. Conductor fight preferred over nearby
		results.Add(d.RunLeg(new HuntLegSpec
		{
			Name = "07-conductor-fight",
			Territory = Zones.Garlemald,
			FlagWorld = d.Position + new Vector3(25f, 0f, 0f),
			FlyZone = true,
			Engage = EngageScriptKind.ConductorFight,
			ExpectedEngage = EngageTargetKind.ConductorFight,
		}));

		// 8. Party fight when no conductor fight
		results.Add(d.RunLeg(new HuntLegSpec
		{
			Name = "08-party-fight",
			Territory = Zones.Garlemald,
			FlagWorld = d.Position + new Vector3(20f, 0f, 5f),
			FlyZone = true,
			Engage = EngageScriptKind.PartyFight,
			ExpectedEngage = EngageTargetKind.PartyFight,
		}));

		// 9. Mid-pipeline AbortThenRestart onto a new flag
		d.Adopt(new HuntLegSpec
		{
			Name = "09a-setup-navigate",
			Territory = Zones.MareLamentorum,
			FlagWorld = new Vector3(50f, 0f, 50f),
			AetheryteWorld = new Vector3(40f, 0f, 40f),
			AetheryteId = 4001,
			FlyZone = true,
		});
		Assert.True(d.Train.IsActive);
		results.Add(d.RunLegReplacingActive(new HuntLegSpec
		{
			Name = "09-abort-restart",
			Territory = Zones.Elpis,
			FlagWorld = new Vector3(300f, 8f, 300f),
			AetheryteWorld = new Vector3(290f, 8f, 290f),
			AetheryteId = 5001,
			FlyZone = true,
			Engage = EngageScriptKind.NearbyARank,
			ExpectedTeleport = TeleportAction.TeleportToZone,
			ExpectedEngage = EngageTargetKind.NearbyARank,
			ExpectedRestart = FlagRestartKind.AbortThenRestart,
		}));

		// 10. Mid-combat flag suppress, then flush after kill
		d.ClearJobsForCombatSetup();
		d.Territory = Zones.Elpis;
		d.Position = new Vector3(300f, 8f, 300f);
		d.DriveToUnmountAt(d.Position + new Vector3(10f, 0f, 0f), Zones.Elpis, flyZone: false);
		d.ForceCombatLatch();
		Assert.True(d.InCombatPhase);
		Assert.Equal(HuntTrainPhase.Combat, d.Train.Phase);

		var deferred = d.RunLeg(new HuntLegSpec
		{
			Name = "10b-deferred-while-combat",
			Territory = Zones.UltimaThule,
			FlagWorld = new Vector3(10f, 0f, 10f),
			AetheryteWorld = new Vector3(0f, 0f, 0f),
			AetheryteId = 6001,
			SuppressWhileFightingARank = true,
			Engage = EngageScriptKind.NearbyARank,
		});
		Assert.True(deferred.Deferred);

		d.EndCombatPublic();
		Assert.Equal(HuntTrainPhase.Idle, d.Train.Phase);
		results.Add(d.RunLeg(new HuntLegSpec
		{
			Name = "10-flush-after-combat",
			Territory = Zones.UltimaThule,
			FlagWorld = new Vector3(10f, 0f, 10f),
			AetheryteWorld = new Vector3(0f, 0f, 0f),
			AetheryteId = 6001,
			FlyZone = true,
			Engage = EngageScriptKind.NearbyARank,
			ExpectedTeleport = TeleportAction.TeleportToZone,
			ExpectedEngage = EngageTargetKind.NearbyARank,
		}));

		// 11. Ground-only zone (no fly on move tick)
		results.Add(d.RunLeg(new HuntLegSpec
		{
			Name = "11-ground-only",
			Territory = Zones.HeritageFound,
			FlagWorld = new Vector3(120f, 0f, 120f),
			AetheryteWorld = new Vector3(100f, 0f, 100f),
			AetheryteId = 7001,
			FlyZone = false,
			Engage = EngageScriptKind.NearbyARank,
			SimulateMeshReadyThenPath = true,
			ExpectedTeleport = TeleportAction.TeleportToZone,
			ExpectedEngage = EngageTargetKind.NearbyARank,
		}));
		Assert.Contains(d.Trace, t => t.Contains("move-tick[start]=StartMeshPath") && t.Contains("fly=False"));

		// 12. Mount skipped (UseMount off) — already-close → StartNavigate
		d.UseMount = false;
		d.Mounted = false;
		d.InFlight = false;
		results.Add(d.RunLeg(new HuntLegSpec
		{
			Name = "12-skip-mount",
			Territory = Zones.HeritageFound,
			FlagWorld = d.Position + new Vector3(20f, 0f, 0f),
			FlyZone = false,
			Engage = EngageScriptKind.NearbyARank,
			ExpectedTeleport = TeleportAction.Skip,
			ExpectedEngage = EngageTargetKind.NearbyARank,
		}));
		Assert.Equal(HuntTrainEvent.StartNavigate, results[^1].Restart.StartEvent);
		Assert.DoesNotContain(HuntTrainPhase.Mount, results[^1].Phases);
		d.UseMount = true;

		// 13. Mesh not ready then path
		results.Add(d.RunLeg(new HuntLegSpec
		{
			Name = "13-mesh-retry-arrive",
			Territory = Zones.HeritageFound,
			FlagWorld = d.Position + new Vector3(35f, 0f, 0f),
			FlyZone = false,
			Engage = EngageScriptKind.NearbyARank,
			SimulateMeshReadyThenPath = true,
			ExpectedEngage = EngageTargetKind.NearbyARank,
		}));
		Assert.Contains(d.Trace, t => t.Contains("move-tick[mesh-not-ready]=Wait") && t.Contains("MeshNotReady"));

		// 14. Remount after combat into next close flag
		d.Mounted = false;
		d.InFlight = false;
		results.Add(d.RunLeg(new HuntLegSpec
		{
			Name = "14-remount-next",
			Territory = Zones.HeritageFound,
			FlagWorld = d.Position + new Vector3(18f, 0f, 0f),
			FlyZone = false,
			Engage = EngageScriptKind.NearbyARank,
			ExpectedRestart = FlagRestartKind.StartFromIdle,
			ExpectedEngage = EngageTargetKind.NearbyARank,
		}));
		Assert.Contains(HuntTrainPhase.Mount, results[^1].Phases);
		Assert.Equal(HuntTrainPhase.Idle, d.Train.Phase);

		Assert.Equal(14, results.Count);
		foreach (var leg in results.Where(r => !r.Deferred))
		{
			AssertExpected(leg);
			Assert.Contains(HuntTrainPhase.Combat, leg.Phases);
			Assert.Equal(HuntTrainPhase.Idle, leg.Phases[^1]);
		}

		Assert.Contains(d.Trace, t => t.Contains("rsr=StartAuto"));
		Assert.Contains(d.Trace, t => t.Contains("rsr-after-end=Stop"));
	}

	private static void AssertExpected(LegResult leg)
	{
		switch (leg.Name)
		{
			case "01-cross-zone-fly":
				Assert.Equal(TeleportAction.TeleportToZone, leg.Teleport.Action);
				Assert.Equal(FlagRestartKind.StartFromIdle, leg.Restart.Kind);
				Assert.Equal(EngageTargetKind.NearbyARank, leg.Engage);
				Assert.Contains(HuntTrainPhase.Teleport, leg.Phases);
				Assert.Contains(HuntTrainPhase.Mount, leg.Phases);
				Assert.Contains(HuntTrainPhase.Navigate, leg.Phases);
				Assert.Contains(HuntTrainPhase.Unmount, leg.Phases);
				break;
			case "02-same-zone-close":
				Assert.Equal(TeleportAction.Skip, leg.Teleport.Action);
				Assert.Equal(EngageTargetKind.NearbyARank, leg.Engage);
				Assert.DoesNotContain(HuntTrainPhase.Teleport, leg.Phases);
				break;
			case "03-same-zone-far":
				Assert.Equal(TeleportAction.TeleportBecauseFar, leg.Teleport.Action);
				break;
			case "04-instance-switch":
				Assert.Equal(TeleportAction.SwitchInstance, leg.Teleport.Action);
				break;
			case "05-mounted-cross-zone":
				Assert.Equal(HuntTrainEvent.StartTeleport, leg.Restart.StartEvent);
				break;
			case "06-divert-mid-nav":
				Assert.DoesNotContain(HuntTrainPhase.Unmount, leg.Phases);
				break;
			case "07-conductor-fight":
				Assert.Equal(EngageTargetKind.ConductorFight, leg.Engage);
				break;
			case "08-party-fight":
				Assert.Equal(EngageTargetKind.PartyFight, leg.Engage);
				break;
			case "09-abort-restart":
				Assert.Equal(FlagRestartKind.AbortThenRestart, leg.Restart.Kind);
				Assert.True(leg.Restart.StopNavPath);
				Assert.True(leg.Restart.ResetTrainController);
				break;
			case "11-ground-only":
				Assert.Equal(TeleportAction.TeleportToZone, leg.Teleport.Action);
				break;
			case "12-skip-mount":
				Assert.Equal(HuntTrainEvent.StartNavigate, leg.Restart.StartEvent);
				break;
		}
	}
}
