#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace HuntTrainAuto;

/// <summary>
/// After flag unmount: join conductor's fight, else a party ally's fight, else nearest A-rank.
/// Does <b>not</b> follow players. Soft-fails; never throws to Framework.
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

	private readonly List<EngageMobCandidate> candidates = [];
	private readonly List<IGameObject> candidateObjects = [];

	private HashSet<uint>? aRankIds;
	private uint? lockedEntityId;
	private EngageTargetKind lastKind = EngageTargetKind.None;

	public EngageTargetHelper(
		IObjectTable objectTable,
		IPartyList partyList,
		ITargetManager targetManager,
		IDataManager dataManager,
		IPluginLog pluginLog,
		MovementHelper movement,
		Func<float> getEngageRange,
		Func<float> getARankScanRange)
	{
		this.objectTable = objectTable;
		this.partyList = partyList;
		this.targetManager = targetManager;
		this.dataManager = dataManager;
		this.pluginLog = pluginLog;
		this.movement = movement;
		this.getEngageRange = getEngageRange;
		this.getARankScanRange = getARankScanRange;
	}

	public EngageTargetKind LastKind => lastKind;

	/// <summary>Clear path / lock (new flag, territory, master off, dispose).</summary>
	public void Clear()
	{
		lockedEntityId = null;
		lastKind = EngageTargetKind.None;
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
	/// <returns>True when within engage range of the chosen mob (enter combat phase).</returns>
	public bool Tick(
		CombatSession combat,
		IList<string> conductors,
		bool pluginEnabled,
		bool playerDead)
	{
		try
		{
			return TickCore(combat, conductors, pluginEnabled, playerDead);
		}
		catch (Exception ex)
		{
			pluginLog.Debug($"EngageTargetHelper soft-fail: {ex.Message}");
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
		bool playerDead)
	{
		if (!pluginEnabled || playerDead)
		{
			Clear();
			if (combat.Phase != CombatPhase.Idle)
				combat.Clear();
			return false;
		}

		// Already in combat phase — do not path; Phase 6 owns rotation.
		if (combat.InCombatPhase)
			return true;

		var player = objectTable.LocalPlayer;
		if (player == null)
			return false;

		EnsureARankIndex();
		BuildCandidates(player, conductors);

		var pick = EngageTargetDecision.Resolve(candidates, getARankScanRange());
		if (!pick.Found || pick.Index < 0 || pick.Index >= candidateObjects.Count)
		{
			lastKind = EngageTargetKind.None;
			lockedEntityId = null;
			return false;
		}

		var mob = candidateObjects[pick.Index];
		lastKind = pick.Kind;
		var entityId = TryEntityId(mob);
		if (entityId != null)
			lockedEntityId = entityId;

		TrySetTarget(mob);

		var dist = Vector3.Distance(player.Position, mob.Position);
		var engageRange = CombatDecision.ClampEngageRange(getEngageRange());

		if (EngageTargetDecision.ShouldEnterCombatOnMob(dist, engageRange))
		{
			movement.Stop();
			combat.Apply(CombatTransitionKind.EnterCombat, entityId);
			pluginLog.Debug($"Engage: EnterCombat via {pick.Kind}");
			return true;
		}

		// Path to mob on foot — not following a player.
		combat.EnterFollowing();
		movement.Move(
			mob.Position,
			tolerance: 1f,
			lastPointTolerance: 2f,
			fly: false,
			useMesh: true);
		return false;
	}

	private void EnsureARankIndex()
	{
		if (aRankIds != null)
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
			pluginLog.Debug($"A-rank sheet soft-fail: {ex.Message}");
		}

		aRankIds = ARankHuntIndex.BuildARankIds(rows);
		pluginLog.Debug($"A-rank index size={aRankIds.Count}");
	}

	private void BuildCandidates(IPlayerCharacter player, IList<string> conductors)
	{
		candidates.Clear();
		candidateObjects.Clear();

		var playerPos = player.Position;
		var aIds = aRankIds ?? [];
		IGameObject? conductorFightTarget = null;
		var partyFightTargets = new HashSet<uint>();

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
			var isConductorPull = conductorFightTarget != null
				&& entityId != null
				&& TryEntityId(conductorFightTarget) == entityId;
			var isPartyPull = entityId != null && partyFightTargets.Contains(entityId.Value);

			// Conductor/party fight targets and A-ranks only (ignore S/B/trash unless join fight).
			if (!isConductorPull && !isPartyPull && !isA)
				continue;

			var index = candidateObjects.Count;
			candidateObjects.Add(obj);
			candidates.Add(new EngageMobCandidate
			{
				Index = index,
				IsConductorFightTarget = isConductorPull,
				IsPartyFightTarget = isPartyPull,
				IsARank = isA,
				Distance = Vector3.Distance(playerPos, obj.Position),
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
			pluginLog.Debug($"Set target soft-fail: {ex.Message}");
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
			return obj.EntityId;
		}
		catch
		{
			return null;
		}
	}
}
