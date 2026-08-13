#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;

namespace HuntTrainAuto.Tests.Scenarios;

/// <summary>
/// Headless Layer-A train driver: pure decisions + <see cref="HuntTrainController"/> ticks.
/// No Dalamud / VNav / Lifestream — positions and mobs are scripted fixtures.
/// </summary>
public sealed class TrainScenarioDriver
{
	public HuntTrainController Train { get; } = new();

	public CombatPhase LocalCombat { get; private set; } = CombatPhase.Idle;

	public bool InCombatPhase => LocalCombat == CombatPhase.Combat;

	public bool RotationAutoStarted { get; private set; }

	public uint Territory { get; set; } = Zones.Labyrinthos;

	public int Instance { get; set; } = 1;

	public Vector3 Position { get; set; } = new(0f, 0f, 0f);

	public bool Mounted { get; set; }

	public bool InFlight { get; set; }

	public bool PluginEnabled { get; set; } = true;

	public bool AutoTeleport { get; set; } = true;

	public bool UseMount { get; set; } = true;

	public int MountConfig { get; set; } // 0 = random (enabled)

	public bool AutoUnmountAtFlag { get; set; } = true;

	public float DistanceThreshold { get; set; } = 150f;

	public float FlagArrivalTolerance { get; set; } = FlagArrival.DefaultTolerance;

	public float EngageRange { get; set; } = CombatDecision.DefaultEngageRange;

	public float ARankScanRange { get; set; } = EngageTargetDecision.DefaultARankScanRange;

	public HuntFlag? ActiveFlag { get; private set; }

	public bool TeleportPlanActive { get; private set; }

	public bool ReadyForGroundFollow { get; set; }

	public bool HuntTargetFound { get; set; }

	public List<HuntTrainPhase> PhaseHistory { get; } = new();

	public List<string> Trace { get; } = new();

	public LegResult? LastLeg { get; private set; }

	public void Note(string message) => Trace.Add(message);

	public void RecordPhase()
	{
		if (PhaseHistory.Count == 0 || PhaseHistory[^1] != Train.Phase)
			PhaseHistory.Add(Train.Phase);
	}

	/// <summary>Adopt a flag and apply restart/start events (mirrors OnHuntFlagReceived core).</summary>
	public FlagRestartPlan Adopt(HuntLegSpec leg)
	{
		ArgumentNullException.ThrowIfNull(leg);

		// Pre-adopt pipeline snapshot (before new TP plan mutates TeleportPlanActive).
		var pipelineActive = FlagRestartDecision.IsPipelineActive(Train.Phase, hasInFlightWork: InCombatPhase);

		var flag = HuntFlag.FromMapLink(
			leg.Territory,
			mapId: leg.Territory,
			rawX: 0,
			rawY: 0,
			placeName: leg.Name);
		flag.WorldPos = leg.FlagWorld;
		flag.ReportedInstance = leg.TargetInstance;
		if (leg.AetheryteId != 0)
		{
			flag.Arrival = ArrivalData.CreateOrNull(
				leg.AetheryteId,
				leg.Territory,
				leg.TargetInstance,
				world: null);
		}

		ActiveFlag = flag;
		ReadyForGroundFollow = false;

		var playerDist = MovementDecision.DistanceXZ(Position, leg.FlagWorld);
		float? aetheryteDist = leg.AetheryteWorld is { } aeth
			? MovementDecision.DistanceXZ(aeth, leg.FlagWorld)
			: null;

		var tp = TeleportDecision.Decide(
			PluginEnabled,
			AutoTeleport,
			Territory,
			leg.Territory,
			playerDist,
			DistanceThreshold,
			Instance,
			leg.TargetInstance,
			flag.Arrival,
			aetheryteDistance: aetheryteDist);

		TeleportPlanActive = tp.ShouldTeleport;
		var alreadyCloseSkip = !tp.ShouldTeleport
			&& Territory == leg.Territory
			&& playerDist <= DistanceThreshold;

		var alreadyMounted = Mounted || InFlight || MountConfig < 0;
		var plan = FlagRestartDecision.Decide(
			PluginEnabled,
			pipelineActive,
			teleportPlanActive: TeleportPlanActive,
			alreadyCloseSkip: alreadyCloseSkip,
			useMount: UseMount && MountConfig >= 0,
			alreadyMountedOrSkipMount: alreadyMounted);

		if (plan.ResetTrainController)
			Train.Reset();
		if (plan.ClearCombat)
			ClearCombat();
		if (plan.ClearRsr)
			RotationAutoStarted = false;

		if (plan.StartEvent != HuntTrainEvent.None)
			Train.Apply(plan.StartEvent);

		RecordPhase();
		Note($"adopt[{leg.Name}] tp={tp.Describe()} restart={plan.Describe()} phase={Train.Phase}");

		LastLeg = new LegResult
		{
			Name = leg.Name,
			Teleport = tp,
			Restart = plan,
			Engage = EngageTargetKind.None,
			Phases = [.. PhaseHistory],
		};
		return plan;
	}

	/// <summary>Run one full hunt leg from adopted flag through combat end → Idle.</summary>
	public LegResult RunLeg(HuntLegSpec leg)
	{
		ArgumentNullException.ThrowIfNull(leg);
		var phaseMark = PhaseHistory.Count;

		if (leg.SuppressWhileFightingARank)
		{
			var suppress = EngageTargetDecision.ShouldSuppressChatWhileFightingARank(
				InCombatPhase,
				targetIsARank: true);
			Note($"suppress-chat={suppress}");
			if (suppress)
			{
				// Flag deferred — do not adopt until combat ends.
				return FinalizeLeg(leg, phaseMark, EngageTargetKind.None, deferred: true);
			}
		}

		Adopt(leg);
		AdvanceMountAndTeleport(leg);
		AdvanceNavigate(leg);
		var engageKind = AdvanceEngageAndCombat(leg);

		AssertIdleAfterCombat();
		return FinalizeLeg(leg, phaseMark, engageKind, deferred: false);
	}

	/// <summary>Enter Following→Combat without finishing (for suppress / mid-combat tests).</summary>
	public void ForceCombatLatch()
	{
		LocalCombat = CombatPhase.Combat;
		if (Train.Phase is HuntTrainPhase.Unmount or HuntTrainPhase.Navigate or HuntTrainPhase.Mount)
		{
			TickProgress();
			RecordPhase();
		}
		else if (Train.Phase == HuntTrainPhase.Idle)
		{
			Train.Apply(HuntTrainEvent.StartNavigate);
			TickProgress();
			RecordPhase();
		}

		EnableRsr();
		Note($"force-combat phase={Train.Phase} local={LocalCombat}");
	}

	public void TickProgressPublic()
	{
		TickProgress();
		RecordPhase();
	}

	public void EndCombatPublic() => EndCombat();

	public void ClearJobsForCombatSetup()
	{
		ClearCombat();
		RotationAutoStarted = false;
		TeleportPlanActive = false;
		ReadyForGroundFollow = false;
		Mounted = false;
		InFlight = false;
		Train.Reset();
		RecordPhase();
	}

	/// <summary>Drive Idle → Mount/Navigate → Unmount at <paramref name="flag"/> (same-zone close).</summary>
	public void DriveToUnmountAt(Vector3 flag, uint territory, bool flyZone)
	{
		Adopt(new HuntLegSpec
		{
			Name = "setup-to-unmount",
			Territory = territory,
			FlagWorld = flag,
			FlyZone = flyZone,
			Engage = EngageScriptKind.NearbyARank,
		});

		if (Train.Phase == HuntTrainPhase.Mount)
		{
			Mounted = true;
			if (flyZone)
				InFlight = true;
			TickProgress();
			RecordPhase();
		}

		if (Train.Phase != HuntTrainPhase.Navigate)
			throw new InvalidOperationException($"Expected Navigate, got {Train.Phase}");

		Position = flag;
		Mounted = false;
		InFlight = false;
		ReadyForGroundFollow = true;
		TickProgress();
		RecordPhase();
		if (Train.Phase != HuntTrainPhase.Unmount)
			throw new InvalidOperationException($"Expected Unmount, got {Train.Phase}");
	}

	/// <summary>
	/// Mid-pipeline abort: adopt a new flag while active (AbortThenRestart), then finish that leg.
	/// </summary>
	public LegResult RunLegReplacingActive(HuntLegSpec leg)
	{
		ArgumentNullException.ThrowIfNull(leg);
		if (!Train.IsActive && !InCombatPhase)
			throw new InvalidOperationException("RunLegReplacingActive requires an active pipeline.");

		return RunLeg(leg);
	}

	private void AdvanceMountAndTeleport(HuntLegSpec leg)
	{
		// Mount-before-TP or same-zone mount.
		if (Train.Phase == HuntTrainPhase.Mount)
		{
			Mounted = true;
			if (leg.FlyZone)
				InFlight = true;
			TickProgress();
			RecordPhase();
			Note($"mount-complete phase={Train.Phase}");
		}

		if (Train.Phase == HuntTrainPhase.Teleport)
		{
			CompleteTeleport(leg);
			TickProgress();
			RecordPhase();
			Note($"teleport-arrived phase={Train.Phase} terr={Territory} inst={Instance}");
		}

		// Idle start may have been StartNavigate / StartMount already handled.
		if (Train.Phase == HuntTrainPhase.Mount)
		{
			Mounted = true;
			if (leg.FlyZone)
				InFlight = true;
			TickProgress();
			RecordPhase();
		}
	}

	private void CompleteTeleport(HuntLegSpec leg)
	{
		Territory = leg.Territory;
		Position = leg.AetheryteWorld ?? leg.FlagWorld;
		TeleportPlanActive = false;

		if (leg.TargetInstance > 0
			&& InstanceChangeDecision.ShouldEnqueueIfNeeded(leg.TargetInstance, Instance))
		{
			Note($"instance-change {Instance}→{leg.TargetInstance}");
			Instance = leg.TargetInstance;
		}
		else if (leg.TargetInstance > 0)
		{
			Instance = leg.TargetInstance;
		}
	}

	private void AdvanceNavigate(HuntLegSpec leg)
	{
		if (Train.Phase != HuntTrainPhase.Navigate)
			return;

		// Pathfind tick sample (mesh wait → start → later arrive) for fly vs ground.
		var dist = MovementDecision.DistanceXZ(Position, leg.FlagWorld);
		var moveWait = MovementDecision.DecideMoveTick(
			playerValid: true,
			flyRequested: leg.FlyZone,
			zoneSupportsFlying: leg.FlyZone,
			mounted: Mounted,
			inFlight: InFlight,
			casting: false,
			destination: leg.FlagWorld,
			distanceToDestination: dist,
			lastPointTolerance: MovementDecision.DefaultTolerance,
			useMesh: true,
			playerReady: true,
			navReady: false,
			pathfindInProgress: false,
			numWaypoints: 0,
			pathIsRunning: false,
			playerOnMesh: true);
		Note($"move-tick[mesh-not-ready]={moveWait.Kind}/{moveWait.WaitReason} fly={moveWait.Fly}");

		if (leg.DivertMidNavigate)
		{
			EnterTrainCombatFromDivert(leg);
			return;
		}

		if (leg.SimulateMeshReadyThenPath)
		{
			var start = MovementDecision.DecideMoveTick(
				playerValid: true,
				flyRequested: leg.FlyZone,
				zoneSupportsFlying: leg.FlyZone,
				mounted: Mounted,
				inFlight: InFlight || (Mounted && leg.FlyZone),
				casting: false,
				destination: leg.FlagWorld,
				distanceToDestination: dist,
				lastPointTolerance: MovementDecision.DefaultTolerance,
				useMesh: true,
				playerReady: true,
				navReady: true,
				pathfindInProgress: false,
				numWaypoints: 0,
				pathIsRunning: false,
				playerOnMesh: true);
			Note($"move-tick[start]={start.Kind} fly={start.Fly}");
		}

		// Instant arrive at flag (Layer A — no real mesh).
		Position = leg.FlagWorld;
		if (leg.FlyZone && Mounted)
		{
			InFlight = true;
			// Descend to floor altitude for FlagArrival in-flight gate.
			Position = new Vector3(leg.FlagWorld.X, leg.FlagWorld.Y, leg.FlagWorld.Z);
		}

		var arrived = FlagArrival.IsArrived(Position, leg.FlagWorld, FlagArrivalTolerance, InFlight);
		Note($"flag-arrival={arrived} dist={MovementDecision.DistanceXZ(Position, leg.FlagWorld):0.##}");

		HuntTargetFound = leg.Engage != EngageScriptKind.None;

		if (AutoUnmountAtFlag && HuntTargetFound && (Mounted || InFlight))
		{
			Mounted = false;
			InFlight = false;
			ReadyForGroundFollow = true;
			Note("unmount-at-flag");
		}
		else if (!(Mounted || InFlight))
		{
			ReadyForGroundFollow = true;
		}

		TickProgress();
		RecordPhase();
		Note($"after-arrive phase={Train.Phase}");
	}

	private void EnterTrainCombatFromDivert(HuntLegSpec leg)
	{
		var mob = leg.FlagWorld + new Vector3(8f, 0f, 0f);
		var dist = MovementDecision.DistanceXZ(Position, mob);
		AssertDivertEligible(dist);

		if (Mounted || InFlight)
		{
			Mounted = false;
			InFlight = false;
			ReadyForGroundFollow = true;
		}

		LocalCombat = CombatPhase.Following;
		LocalCombat = CombatPhase.Combat;
		TickProgress(); // PartyEngaged → EnterCombat
		RecordPhase();
		Note($"divert-engage phase={Train.Phase} dist={dist:0.##}");
	}

	private EngageTargetKind AdvanceEngageAndCombat(HuntLegSpec leg)
	{
		if (Train.Phase == HuntTrainPhase.Combat && leg.DivertMidNavigate)
		{
			// Divert already entered combat; still resolve pick for assertions.
			var divertPick = ResolveEngage(leg);
			EnableRsr();
			EndCombat();
			return divertPick.Kind;
		}

		if (Train.Phase is not (HuntTrainPhase.Unmount or HuntTrainPhase.Navigate or HuntTrainPhase.Mount))
		{
			if (Train.Phase == HuntTrainPhase.Idle)
				return EngageTargetKind.None;
		}

		// Stay Unmount until PartyEngaged.
		var pick = ResolveEngage(leg);
		Note($"engage={EngageTargetDecision.Describe(pick)}");

		if (!pick.Found)
			throw new InvalidOperationException($"Leg '{leg.Name}' expected an engage target.");

		// EngageTargetHelper owns EnterCombat from mob approach — CombatDecision.Decide
		// does not EnterCombat from Idle; Following→Combat needs a fighting snap.
		LocalCombat = CombatPhase.Following;
		var enterSnap = BuildCombatSnap(inCombat: true, latchedAlive: true);
		var enter = CombatDecision.Decide(LocalCombat, enterSnap);
		LocalCombat = CombatDecision.NextPhase(LocalCombat, enter);
		if (LocalCombat != CombatPhase.Combat)
			LocalCombat = CombatPhase.Combat; // engage-on-mob path
		Note($"combat-local={LocalCombat} via={enter}");

		TickProgress(); // PartyEngaged while Unmount/Navigate/Mount
		RecordPhase();

		EnableRsr();
		EndCombat();
		return pick.Kind;
	}

	private EngageTargetPick ResolveEngage(HuntLegSpec leg)
	{
		var candidates = BuildCandidates(leg);
		return EngageTargetDecision.Resolve(candidates, ARankScanRange, preferNearHint: true);
	}

	private List<EngageMobCandidate> BuildCandidates(HuntLegSpec leg)
	{
		var list = new List<EngageMobCandidate>();
		var flag = ActiveFlag?.WorldPos ?? leg.FlagWorld;

		void Add(int index, EngageScriptKind kind, Vector3 mobPos, bool isA = true)
		{
			var playerDist = MovementDecision.DistanceXZ(Position, mobPos);
			var flagDist = MovementDecision.DistanceXZ(flag, mobPos);
			list.Add(new EngageMobCandidate
			{
				Index = index,
				IsConductorFightTarget = kind == EngageScriptKind.ConductorFight,
				IsPartyFightTarget = kind == EngageScriptKind.PartyFight,
				IsARank = isA,
				Distance = playerDist,
				EligibilityDistance = EngageTargetDecision.EligibilityDistance(playerDist, flagDist),
				DistanceToHint = flagDist,
				IsAlive = true,
			});
		}

		switch (leg.Engage)
		{
			case EngageScriptKind.ConductorFight:
				Add(0, EngageScriptKind.NearbyARank, flag + new Vector3(30f, 0f, 0f));
				Add(1, EngageScriptKind.ConductorFight, flag + new Vector3(12f, 0f, 0f));
				break;
			case EngageScriptKind.PartyFight:
				Add(0, EngageScriptKind.NearbyARank, flag + new Vector3(40f, 0f, 0f));
				Add(1, EngageScriptKind.PartyFight, flag + new Vector3(10f, 0f, 0f));
				break;
			case EngageScriptKind.NearbyARank:
			case EngageScriptKind.DivertNearbyARank:
				Add(0, EngageScriptKind.NearbyARank, flag + new Vector3(6f, 0f, 0f));
				break;
			case EngageScriptKind.None:
				break;
			default:
				Add(0, EngageScriptKind.NearbyARank, flag + new Vector3(6f, 0f, 0f));
				break;
		}

		return list;
	}

	private void EnableRsr()
	{
		var kind = RsrEnableDecision.Decide(InCombatPhase, RotationAutoStarted);
		Note($"rsr={kind}");
		if (kind == RsrEnableKind.StartAuto)
			RotationAutoStarted = RsrEnableDecision.NextRotationAutoStarted(kind, ipcSucceeded: true, RotationAutoStarted);
	}

	private void EndCombat()
	{
		var snap = BuildCombatSnap(inCombat: false, latchedAlive: false);
		var kind = CombatDecision.Decide(LocalCombat, snap);
		LocalCombat = CombatDecision.NextPhase(LocalCombat, kind);
		Note($"combat-end local={LocalCombat} via={kind}");

		TickProgress(); // CombatEnded
		RecordPhase();

		var rsrStop = RsrEnableDecision.Decide(InCombatPhase, RotationAutoStarted);
		Note($"rsr-after-end={rsrStop}");
		if (rsrStop == RsrEnableKind.Stop)
			RotationAutoStarted = RsrEnableDecision.NextRotationAutoStarted(rsrStop, ipcSucceeded: true, RotationAutoStarted);
	}

	private void ClearCombat()
	{
		LocalCombat = CombatPhase.Idle;
	}

	private void AssertIdleAfterCombat()
	{
		if (Train.Phase != HuntTrainPhase.Idle)
			throw new InvalidOperationException($"Expected Idle after combat, got {Train.Phase}");
	}

	private void AssertDivertEligible(float dist)
	{
		if (!EngageTargetDecision.ShouldDivertFromFlagNav(dist, ARankScanRange))
			throw new InvalidOperationException($"Divert distance {dist} outside scan range.");
	}

	private CombatEngageSnapshot BuildCombatSnap(bool inCombat, bool latchedAlive)
		=> new()
		{
			PluginEnabled = PluginEnabled,
			PlayerDead = false,
			PartyTargetsHuntMob = false,
			DistanceToPartyHuntMob = null,
			PlayerInCombat = inCombat,
			AnyPartyAllyInCombat = false,
			LatchedEngageTargetInCombat = latchedAlive && inCombat,
			LatchedEngageTargetAlive = latchedAlive,
			NearbyEngageTargetAlive = latchedAlive,
			HoldCombatPhase = false,
			EngageRange = EngageRange,
		};

	private void TickProgress()
	{
		var snap = HuntTrainObserve.BuildProgressSnapshot(
			pluginEnabled: PluginEnabled,
			abort: false,
			teleportPlanActive: TeleportPlanActive,
			mountJobActive: false,
			mounted: Mounted,
			inFlight: InFlight,
			mountConfig: MountConfig,
			withinFlagArrival: ActiveFlag?.WorldPos is { } wp
				&& FlagArrival.IsArrived(Position, wp, FlagArrivalTolerance, InFlight),
			huntTargetFound: HuntTargetFound,
			autoUnmountAtFlag: AutoUnmountAtFlag,
			readyForGroundFollow: ReadyForGroundFollow,
			inCombatPhase: InCombatPhase);
		Train.Tick(snap);
	}

	private LegResult FinalizeLeg(HuntLegSpec leg, int phaseMark, EngageTargetKind engage, bool deferred)
	{
		var phases = PhaseHistory.GetRange(phaseMark, PhaseHistory.Count - phaseMark);
		var result = new LegResult
		{
			Name = leg.Name,
			Teleport = LastLeg?.Teleport ?? default,
			Restart = LastLeg?.Restart ?? default,
			Engage = engage,
			Phases = phases,
			Deferred = deferred,
		};
		LastLeg = result;
		Note($"leg-done[{leg.Name}] engage={engage} deferred={deferred} phases=[{string.Join("→", phases)}]");
		return result;
	}
}

public enum EngageScriptKind
{
	None,
	NearbyARank,
	ConductorFight,
	PartyFight,
	DivertNearbyARank,
}

public sealed class HuntLegSpec
{
	public required string Name { get; init; }

	public required uint Territory { get; init; }

	public required Vector3 FlagWorld { get; init; }

	/// <summary>0 = unspecified (no instance switch).</summary>
	public int TargetInstance { get; init; }

	public uint AetheryteId { get; init; } = 1001;

	public Vector3? AetheryteWorld { get; init; }

	public bool FlyZone { get; init; } = true;

	public EngageScriptKind Engage { get; init; } = EngageScriptKind.NearbyARank;

	public bool DivertMidNavigate { get; init; }

	public bool SimulateMeshReadyThenPath { get; init; }

	/// <summary>When true, adopt while already fighting an A — expect deferral.</summary>
	public bool SuppressWhileFightingARank { get; init; }

	public TeleportAction? ExpectedTeleport { get; init; }

	public EngageTargetKind? ExpectedEngage { get; init; }

	public FlagRestartKind? ExpectedRestart { get; init; }
}

public sealed class LegResult
{
	public required string Name { get; init; }

	public TeleportDecisionResult Teleport { get; init; }

	public FlagRestartPlan Restart { get; init; }

	public EngageTargetKind Engage { get; init; }

	public required IReadOnlyList<HuntTrainPhase> Phases { get; init; }

	public bool Deferred { get; init; }
}

/// <summary>Stable synthetic territory ids for fixtures (not real RowIds).</summary>
public static class Zones
{
	public const uint Labyrinthos = 956;
	public const uint Thavnair = 957;
	public const uint Garlemald = 958;
	public const uint MareLamentorum = 959;
	public const uint Elpis = 960;
	public const uint UltimaThule = 961;
	public const uint HeritageFound = 1187;
	public const uint Shaaloani = 1188;
}
