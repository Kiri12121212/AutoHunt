#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using HuntTrainAuto.Logging;
using Lumina.Excel.Sheets;

namespace HuntTrainAuto.Combat;

/// <summary>Soft probe result for divert / land-unmount (no side effects).</summary>
public readonly struct EngageProbeResult
{
	public static EngageProbeResult None { get; } = new()
	{
		Found = false,
		Kind = EngageTargetKind.None,
		Distance = float.PositiveInfinity,
		EligibilityDistance = float.PositiveInfinity,
		MobPosition = Vector3.Zero,
		IsARank = false,
	};

	public required bool Found { get; init; }

	public required EngageTargetKind Kind { get; init; }

	/// <summary>Player → mob distance (yalms). Use for land / engage proximity.</summary>
	public required float Distance { get; init; }

	/// <summary>
	/// Flag-centered eligibility distance (yalms). Use for divert / scan-range checks.
	/// </summary>
	public required float EligibilityDistance { get; init; }

	public required Vector3 MobPosition { get; init; }

	public required bool IsARank { get; init; }
}

/// <summary>
/// After flag unmount: join conductor's A-rank fight, else a party ally's A-rank fight,
/// else nearby A-rank (optionally biased toward last Sonar/HA/conductor position hint).
/// Does <b>not</b> follow players or engage trash/S/B. Soft-fails; never throws to Framework.
/// Sonar is a soft dependency (chat map-links only — no public Sonar IPC).
/// </summary>
public sealed class EngageTargetHelper
{
	private readonly IObjectTable objectTable;
	private readonly IPartyList partyList;
	private readonly ITargetManager targetManager;
	private readonly IDataManager dataManager;
	private readonly IPluginLog pluginLog;
	private readonly MovementHelper movement;
	private readonly Func<float> getEngageRange;
	private readonly Func<float> getARankScanRange;
	private readonly Func<bool> getPreferNearHint;
	private readonly Func<Vector3?> getPositionHint;
	private readonly Func<Vector3?> getFakeARankWorldPos;
	private readonly System.Action? onFakeARankEnteredCombat;

	private readonly List<EngageMobCandidate> candidates = [];
	private readonly List<IGameObject> candidateObjects = [];

	private ARankIdIndex? aRankIndex;
	private uint? lockedEntityId;
	private EngageTargetKind lastKind = EngageTargetKind.None;
	private bool lastTargetIsARank;

	public EngageTargetHelper(
		IObjectTable objectTable,
		IPartyList partyList,
		ITargetManager targetManager,
		IDataManager dataManager,
		IPluginLog pluginLog,
		MovementHelper movement,
		Func<float> getEngageRange,
		Func<float> getARankScanRange,
		Func<bool>? getPreferNearHint = null,
		Func<Vector3?>? getPositionHint = null,
		Func<Vector3?>? getFakeARankWorldPos = null,
		System.Action? onFakeARankEnteredCombat = null)
	{
		this.objectTable = objectTable;
		this.partyList = partyList;
		this.targetManager = targetManager;
		this.dataManager = dataManager;
		this.pluginLog = pluginLog;
		this.movement = movement;
		this.getEngageRange = getEngageRange;
		this.getARankScanRange = getARankScanRange;
		this.getPreferNearHint = getPreferNearHint ?? (() => false);
		this.getPositionHint = getPositionHint ?? (() => null);
		this.getFakeARankWorldPos = getFakeARankWorldPos ?? (() => null);
		this.onFakeARankEnteredCombat = onFakeARankEnteredCombat;
	}

	public EngageTargetKind LastKind => lastKind;

	/// <summary>
	/// True when the last resolved engage mob is NotoriousMonster Rank A
	/// (any engage kind). Used to ignore chat flags mid-fight.
	/// </summary>
	public bool TargetIsARank => lastTargetIsARank;

	/// <summary>
	/// Soft probe for a nearby engage mob without pathing or EnterCombat.
	/// Used to divert flag Navigate → land / unmount when the pull is already close.
	/// </summary>
	/// <param name="flagWorldPos">
	/// Active conductor flag WorldPos when known; enables flag-centered A-rank eligibility.
	/// </param>
	public EngageProbeResult Probe(IList<string> conductors, Vector3? flagWorldPos = null)
	{
		try
		{
			var player = objectTable.LocalPlayer;
			if (player == null)
				return EngageProbeResult.None;

			// Fake Hunt synthetic A wins so divert/unmount is deterministic without a live mob.
			if (TryFakeARankProbe(player.Position, flagWorldPos, out var fakeProbe))
				return fakeProbe;

			EnsureARankIndex();
			BuildCandidates(player, conductors, flagWorldPos);

			var pick = EngageTargetDecision.Resolve(
				candidates,
				getARankScanRange(),
				getPreferNearHint());
			if (!pick.Found || pick.Index < 0 || pick.Index >= candidateObjects.Count)
				return EngageProbeResult.None;

			var mob = candidateObjects[pick.Index];
			var candidate = candidates[pick.Index];
			return new EngageProbeResult
			{
				Found = true,
				Kind = pick.Kind,
				Distance = candidate.Distance,
				EligibilityDistance = candidate.EligibilityDistance,
				MobPosition = mob.Position,
				IsARank = candidate.IsARank,
			};
		}
		catch (Exception ex)
		{
			pluginLog.Debug($"[Engage] probe soft-fail: {ex.Message}");
			return EngageProbeResult.None;
		}
	}

	/// <summary>Clear path / lock (new flag, territory, master off, dispose).</summary>
	public void Clear()
	{
		lockedEntityId = null;
		lastKind = EngageTargetKind.None;
		lastTargetIsARank = false;
		try
		{
			movement.Stop();
		}
		catch
		{
			// soft-fail
		}
	}

	/// <summary>
	/// Resolve engage target, hard-target it, path ground to it.
	/// When within engage range → <see cref="CombatSession.EnterCombat"/> via return / caller.
	/// </summary>
	/// <param name="flagWorldPos">
	/// Active conductor flag WorldPos when known; enables flag-centered A-rank eligibility.
	/// </param>
	/// <returns>True when within engage range of the chosen mob (enter combat phase).</returns>
	public bool Tick(
		CombatSession combat,
		IList<string> conductors,
		bool pluginEnabled,
		bool playerDead,
		Vector3? flagWorldPos = null)
	{
		try
		{
			return TickCore(combat, conductors, pluginEnabled, playerDead, flagWorldPos);
		}
		catch (Exception ex)
		{
			pluginLog.Debug($"[Engage] tick soft-fail: {ex.Message}");
			try
			{
				Clear();
			}
			catch
			{
				// ignore
			}

			return false;
		}
	}

	private bool TickCore(
		CombatSession combat,
		IList<string> conductors,
		bool pluginEnabled,
		bool playerDead,
		Vector3? flagWorldPos)
	{
		if (!pluginEnabled || playerDead)
		{
			Clear();
			if (combat.Phase != CombatPhase.Idle)
				combat.Clear();
			return false;
		}

		// Already in combat phase — keep vnav stopped; BossMod AI owns positioning.
		// Re-assert hard-target on the locked A-rank so RSR cannot tab trash.
		if (combat.InCombatPhase)
		{
			try
			{
				movement.Stop();
			}
			catch
			{
				// soft-fail
			}

			TryRetargetLockedARank();
			return true;
		}

		var player = objectTable.LocalPlayer;
		if (player == null)
			return false;

		if (TryTickFakeARank(combat, player.Position, flagWorldPos))
			return combat.InCombatPhase;

		EnsureARankIndex();
		BuildCandidates(player, conductors, flagWorldPos);

		var pick = EngageTargetDecision.Resolve(
			candidates,
			getARankScanRange(),
			getPreferNearHint());
		if (!pick.Found || pick.Index < 0 || pick.Index >= candidateObjects.Count)
		{
			lastKind = EngageTargetKind.None;
			lastTargetIsARank = false;
			lockedEntityId = null;
			return false;
		}

		var mob = candidateObjects[pick.Index];
		lastKind = pick.Kind;
		lastTargetIsARank = candidates[pick.Index].IsARank;
		var entityId = TryEntityId(mob);
		if (entityId != null)
			lockedEntityId = entityId;

		TrySetTarget(mob);

		var dist = candidates[pick.Index].Distance;
		var engageRange = CombatDecision.ClampEngageRange(getEngageRange());

		if (EngageTargetDecision.ShouldEnterCombatOnMob(dist, engageRange))
		{
			// Hand off to BossMod / RSR — do not keep pathing into melee.
			movement.Stop();
			combat.Apply(CombatTransitionKind.EnterCombat, entityId);
			pluginLog.Information(
				$"[Engage] {CombatDecision.Describe(CombatTransitionKind.EnterCombat)} via {pick.Kind} "
				+ $"dist={dist:0.0} range={engageRange:0.0} entity={entityId?.ToString() ?? "none"} (vnav stop)");
			return true;
		}

		// Path toward mob on foot until engage range.
		combat.EnterFollowing();
		if (DebugThrottle.Try("engage.following", 2000, Environment.TickCount64))
		{
			pluginLog.Debug(
				$"[Engage] following {EngageTargetDecision.Describe(pick)} "
				+ $"dist={dist:0.0} range={engageRange:0.0}");
		}
		movement.Move(
			mob.Position,
			tolerance: 1f,
			lastPointTolerance: Math.Max(2f, engageRange),
			fly: false,
			useMesh: true);
		return false;
	}

	private bool TryFakeARankProbe(
		Vector3 playerPos,
		Vector3? flagWorldPos,
		out EngageProbeResult result)
	{
		result = EngageProbeResult.None;
		Vector3? fakePos;
		try
		{
			fakePos = getFakeARankWorldPos();
		}
		catch
		{
			return false;
		}

		if (fakePos is not { } mob || mob == Vector3.Zero)
			return false;

		var playerToMob = Vector3.Distance(playerPos, mob);
		float? flagToMob = null;
		if (flagWorldPos is { } fp && fp != Vector3.Zero)
			flagToMob = Vector3.Distance(fp, mob);

		result = new EngageProbeResult
		{
			Found = true,
			Kind = EngageTargetKind.NearbyARank,
			Distance = playerToMob,
			EligibilityDistance = EngageTargetDecision.EligibilityDistance(playerToMob, flagToMob),
			MobPosition = mob,
			IsARank = true,
		};
		return true;
	}

	/// <summary>
	/// Path / EnterCombat against Fake Hunt synthetic A (no object-table entity / no hard-target).
	/// </summary>
	private bool TryTickFakeARank(CombatSession combat, Vector3 playerPos, Vector3? flagWorldPos)
	{
		if (!TryFakeARankProbe(playerPos, flagWorldPos, out var probe) || !probe.Found)
			return false;

		lastKind = EngageTargetKind.NearbyARank;
		lastTargetIsARank = true;
		lockedEntityId = null;

		var engageRange = CombatDecision.ClampEngageRange(getEngageRange());
		if (EngageTargetDecision.ShouldEnterCombatOnMob(probe.Distance, engageRange))
		{
			if (combat.InCombatPhase)
				return true;

			movement.Stop();
			combat.Apply(CombatTransitionKind.EnterCombat, latchedEngageEntityId: null);
			pluginLog.Information(
				$"[FakeHunt] Engage: EnterCombat via NearbyARank dist={probe.Distance:0.0} "
				+ $"range={engageRange:0.0} (synthetic A)");
			try
			{
				onFakeARankEnteredCombat?.Invoke();
			}
			catch
			{
				// soft-fail notify
			}

			return true;
		}

		combat.EnterFollowing();
		if (DebugThrottle.Try("engage.fakeFollow", 2000, Environment.TickCount64))
		{
			pluginLog.Debug(
				$"[FakeHunt] Following synthetic A dist={probe.Distance:0.0} range={engageRange:0.0}");
		}

		movement.Move(
			probe.MobPosition,
			tolerance: 1f,
			lastPointTolerance: Math.Max(2f, engageRange),
			fly: false,
			useMesh: true);
		return true;
	}

	private void EnsureARankIndex()
	{
		if (aRankIndex != null)
			return;

		var rows = new List<(uint, uint, byte)>();
		try
		{
			var sheet = dataManager.GetExcelSheet<NotoriousMonster>();
			if (sheet != null)
			{
				foreach (var row in sheet)
				{
					rows.Add((
						row.BNpcName.RowId,
						row.BNpcBase.RowId,
						row.Rank));
				}
			}
		}
		catch (Exception ex)
		{
			pluginLog.Debug($"[Engage] A-rank sheet soft-fail: {ex.Message}");
		}

		aRankIndex = ARankHuntIndex.BuildARankIds(rows);
		pluginLog.Debug(
			$"[Engage] A-rank index names={aRankIndex.Value.NameIds.Count} "
			+ $"bases={aRankIndex.Value.BaseIds.Count}");
	}

	private void BuildCandidates(
		IPlayerCharacter player,
		IList<string> conductors,
		Vector3? flagWorldPos)
	{
		candidates.Clear();
		candidateObjects.Clear();

		var playerPos = player.Position;
		var flagPos = flagWorldPos is { } fp && fp != Vector3.Zero ? fp : (Vector3?)null;
		var aIds = aRankIndex ?? ARankIdIndex.Empty;
		IGameObject? conductorFightTarget = null;
		var partyFightTargets = new HashSet<uint>();
		Vector3? hintPos = null;
		try
		{
			hintPos = getPositionHint();
			if (hintPos is { } hp && hp == Vector3.Zero)
				hintPos = null;
		}
		catch
		{
			hintPos = null;
		}

		foreach (var obj in objectTable)
		{
			if (obj is not IPlayerCharacter pc)
				continue;
			if (!TryIsValid(pc) || pc.EntityId == player.EntityId)
				continue;

			var name = GetName(pc);
			if (!ChatSender.IsConductor(conductors, name))
				continue;

			if (!IsInCombat(pc))
				continue;

			if (TryGetLivingBattleNpc(pc.TargetObject, out var pull) && pull != null)
			{
				conductorFightTarget = pull;
				break;
			}
		}

		CollectPartyFightTargetIds(player.EntityId, partyFightTargets);

		foreach (var obj in objectTable)
		{
			if (obj is not IBattleNpc)
				continue;
			if (!TryIsValid(obj))
				continue;
			if (obj.ObjectKind != ObjectKind.BattleNpc)
				continue;
			if (obj is ICharacter { CurrentHp: <= 0 })
				continue;

			var nameId = obj is ICharacter ch ? ch.NameId : 0u;
			var baseId = obj.BaseId;
			var isA = ARankHuntIndex.IsARank(aIds, nameId, baseId);
			var entityId = TryEntityId(obj);
			// A-ranks only — never hard-target trash/S/B even if conductor/party tab them.
			// NameId vs BaseId are separate namespaces (see ARankHuntIndex).
			if (!isA)
				continue;

			var isConductorPull = conductorFightTarget != null
				&& entityId != null
				&& TryEntityId(conductorFightTarget) == entityId;
			var isPartyPull = entityId != null && partyFightTargets.Contains(entityId.Value);

			var playerDist = Vector3.Distance(playerPos, obj.Position);
			float? flagDist = flagPos is { } flag
				? Vector3.Distance(flag, obj.Position)
				: null;

			var index = candidateObjects.Count;
			candidateObjects.Add(obj);
			var distToHint = hintPos is { } hint
				? EngagePositionHint.DistanceXZ(obj.Position, hint)
				: float.PositiveInfinity;
			candidates.Add(new EngageMobCandidate
			{
				Index = index,
				IsConductorFightTarget = isConductorPull,
				IsPartyFightTarget = isPartyPull,
				IsARank = isA,
				Distance = playerDist,
				EligibilityDistance = EngageTargetDecision.EligibilityDistance(playerDist, flagDist),
				DistanceToHint = distToHint,
				IsAlive = true,
			});
		}
	}

	/// <summary>
	/// Party members in combat → their living BattleNpc targets (EntityIds).
	/// Same object-table limit as conductor: ally must be loaded nearby.
	/// </summary>
	private void CollectPartyFightTargetIds(uint localEntityId, HashSet<uint> into)
	{
		try
		{
			for (var i = 0; i < partyList.Length; i++)
			{
				var member = partyList[i];
				if (member == null)
					continue;

				IGameObject? obj;
				try
				{
					obj = member.GameObject;
				}
				catch
				{
					continue;
				}

				if (obj == null || !TryIsValid(obj))
					continue;
				if (TryEntityId(obj) == localEntityId)
					continue;
				if (obj is not ICharacter ally || !IsInCombat(ally))
					continue;

				if (TryGetLivingBattleNpc(obj.TargetObject, out var pull)
					&& pull != null
					&& TryEntityId(pull) is { } pullId)
				{
					into.Add(pullId);
				}
			}
		}
		catch
		{
			// soft-fail: empty party fight set
		}
	}

	/// <summary>
	/// While latched on an A-rank, keep the hard-target on that entity.
	/// Stops RSR / tab from parking on trash mid-fight.
	/// </summary>
	private void TryRetargetLockedARank()
	{
		var id = lockedEntityId;
		if (id == null)
			return;

		try
		{
			if (targetManager.Target is { } cur
			    && TryEntityId(cur) == id.Value
			    && cur is ICharacter { CurrentHp: > 0 })
				return;

			EnsureARankIndex();
			var aIds = aRankIndex ?? ARankIdIndex.Empty;

			foreach (var obj in objectTable)
			{
				if (!TryIsValid(obj) || TryEntityId(obj) != id.Value)
					continue;
				if (obj.ObjectKind != ObjectKind.BattleNpc)
					return;
				if (obj is ICharacter { CurrentHp: <= 0 })
				{
					lockedEntityId = null;
					return;
				}

				var nameId = obj is ICharacter ch ? ch.NameId : 0u;
				if (!ARankHuntIndex.IsARank(aIds, nameId, obj.BaseId))
				{
					// Lock pointed at a non-A (stale / false-positive) — drop it.
					lockedEntityId = null;
					return;
				}

				TrySetTarget(obj);
				return;
			}
		}
		catch (Exception ex)
		{
			pluginLog.Debug($"[Engage] retarget locked A soft-fail: {ex.Message}");
		}
	}

	private void TrySetTarget(IGameObject mob)
	{
		try
		{
			if (targetManager.Target?.EntityId == mob.EntityId)
				return;
			targetManager.Target = mob;
		}
		catch (Exception ex)
		{
			pluginLog.Debug($"[Engage] set target soft-fail: {ex.Message}");
		}
	}

	private static bool TryGetLivingBattleNpc(IGameObject? source, out IGameObject? battleNpc)
	{
		battleNpc = null;
		if (source == null)
			return false;
		try
		{
			if (!source.IsValid())
				return false;
			if (source.ObjectKind != ObjectKind.BattleNpc)
				return false;
			if (source is ICharacter { CurrentHp: <= 0 })
				return false;
			battleNpc = source;
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static bool IsInCombat(ICharacter character)
	{
		try
		{
			return (character.StatusFlags & StatusFlags.InCombat) != 0;
		}
		catch
		{
			return false;
		}
	}

	private static string GetName(IGameObject obj)
	{
		try
		{
			return obj.Name.TextValue ?? string.Empty;
		}
		catch
		{
			return string.Empty;
		}
	}

	private static bool TryIsValid(IGameObject obj)
	{
		try
		{
			return obj.IsValid();
		}
		catch
		{
			return false;
		}
	}

	private static uint? TryEntityId(IGameObject? obj)
	{
		if (obj == null)
			return null;
		try
		{
			var id = obj.EntityId;
			if (id == 0 || id == 0xE0000000)
				return null;
			return id;
		}
		catch
		{
			return null;
		}
	}
}
