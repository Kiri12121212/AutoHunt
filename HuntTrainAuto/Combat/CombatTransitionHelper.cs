#nullable enable
using System;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using HuntTrainAuto.Logging;

namespace HuntTrainAuto.Combat;

/// <summary>
/// Thin Framework wiring for combat transition (TASKS 5.8–5.9 / brief 5.4).
/// Builds <see cref="CombatEngageSnapshot"/> → <see cref="CombatDecision"/> →
/// update <see cref="CombatSession"/>. Soft-fails; never throws to Framework.
/// EnterCombat is owned by <see cref="EngageTargetHelper"/>; this helper exits combat.
/// RSR enable is owned by <see cref="RsrEnableHelper"/> (Phase 6.2).
/// </summary>
public sealed class CombatTransitionHelper
{
	private readonly CombatSession session = new();
	private readonly IObjectTable objectTable;
	private readonly IPartyList partyList;
	private readonly ICondition condition;
	private readonly IPluginLog pluginLog;
	private readonly Func<float> getEngageRange;
	private readonly Func<bool> holdCombatPhase;
	private readonly Func<bool> nearbyEngageAlive;

	public CombatTransitionHelper(
		IObjectTable objectTable,
		IPartyList partyList,
		ICondition condition,
		IPluginLog pluginLog,
		Func<float> getEngageRange,
		Func<bool>? holdCombatPhase = null,
		Func<bool>? nearbyEngageAlive = null)
	{
		this.objectTable = objectTable;
		this.partyList = partyList;
		this.condition = condition;
		this.pluginLog = pluginLog;
		this.getEngageRange = getEngageRange;
		this.holdCombatPhase = holdCombatPhase ?? (() => false);
		this.nearbyEngageAlive = nearbyEngageAlive ?? (() => false);
	}

	/// <summary>Phase latch — Phase 6 observes <see cref="CombatSession.InCombatPhase"/>.</summary>
	public CombatSession Session => session;

	/// <summary>Convenience: <see cref="CombatSession.InCombatPhase"/>.</summary>
	public bool InCombatPhase => session.InCombatPhase;

	/// <summary>Abort to Idle (new flag / territory / dispose / master off).</summary>
	public void Clear() => session.Clear();

	/// <summary>
	/// One Framework tick: decide transition (exit combat / death / master off).
	/// EnterCombat is owned by <see cref="EngageTargetHelper"/>.
	/// </summary>
	public void Tick(bool pluginEnabled)
	{
		try
		{
			TickCore(pluginEnabled);
		}
		catch (Exception ex)
		{
			pluginLog.Debug($"[Combat] transition soft-fail: {ex.Message}");
		}
	}

	private void TickCore(bool pluginEnabled)
	{
		var snap = BuildSnapshot(pluginEnabled);
		var kind = CombatDecision.Decide(session.Phase, snap);

		if (kind == CombatTransitionKind.StopFollow && session.Phase != CombatPhase.Idle)
		{
			pluginLog.Debug(
				$"[Combat] {CombatDecision.Describe(kind)} from {session.Phase} "
				+ $"playerInCombat={snap.PlayerInCombat} "
				+ $"latchedAlive={snap.LatchedEngageTargetAlive} "
				+ $"latchedMob={snap.LatchedEngageTargetInCombat} "
				+ $"nearbyA={snap.NearbyEngageTargetAlive} "
				+ $"hold={snap.HoldCombatPhase} dead={snap.PlayerDead}");
		}
		else if (kind == CombatTransitionKind.StayFollow
		         && session.Phase == CombatPhase.Combat
		         && (snap.HoldCombatPhase || snap.NearbyEngageTargetAlive || snap.LatchedEngageTargetAlive)
		         && DebugThrottle.Try("combat.hold", 2000, Environment.TickCount64))
		{
			pluginLog.Debug(
				"[Combat] holding phase "
				+ $"(hold={snap.HoldCombatPhase} nearbyA={snap.NearbyEngageTargetAlive} "
				+ $"latchedAlive={snap.LatchedEngageTargetAlive} inCombat={snap.PlayerInCombat})");
		}

		session.Apply(kind);
	}

	private CombatEngageSnapshot BuildSnapshot(bool pluginEnabled)
	{
		var player = objectTable.LocalPlayer;
		var playerDead = IsPlayerDead(player);
		// ConditionFlag only — StatusFlags.InCombat lingers ~7–10s after the
		// condition clears and was delaying StopFollow / deferred flag flush / TP.
		var playerInCombat = !playerDead && condition[ConditionFlag.InCombat];

		var partyTargets = false;
		float? distPartyMob = null;
		var anyAllyInCombat = false;
		ScanParty(
			player,
			playerInCombat,
			ref partyTargets,
			ref distPartyMob,
			ref anyAllyInCombat);

		ResolveLatchedEngage(out var latchedAlive, out var latchedInCombat);
		var hold = false;
		try
		{
			hold = holdCombatPhase();
		}
		catch
		{
			hold = false;
		}

		var nearbyA = false;
		// Only probe while in Combat — avoid scanning every Idle tick.
		if (session.Phase == CombatPhase.Combat)
		{
			try
			{
				nearbyA = nearbyEngageAlive();
			}
			catch
			{
				nearbyA = false;
			}
		}

		return new CombatEngageSnapshot
		{
			PluginEnabled = pluginEnabled,
			PlayerDead = playerDead,
			PartyTargetsHuntMob = partyTargets,
			DistanceToPartyHuntMob = distPartyMob,
			PlayerInCombat = playerInCombat,
			AnyPartyAllyInCombat = anyAllyInCombat,
			LatchedEngageTargetAlive = latchedAlive,
			LatchedEngageTargetInCombat = latchedInCombat,
			NearbyEngageTargetAlive = nearbyA,
			HoldCombatPhase = hold,
			EngageRange = CombatDecision.ClampEngageRange(getEngageRange()),
		};
	}

	/// <summary>
	/// Living latched mob holds Combat through pre-pull; InCombat is secondary.
	/// Dead / despawned → both false so remount can fire after the kill.
	/// </summary>
	private void ResolveLatchedEngage(out bool alive, out bool inCombat)
	{
		alive = false;
		inCombat = false;
		var id = session.LatchedEngageEntityId;
		if (id == null)
			return;

		try
		{
			foreach (var obj in objectTable)
			{
				if (!TryIsValid(obj) || TryEntityId(obj) != id.Value)
					continue;
				if (obj is not ICharacter ch || ch.CurrentHp <= 0)
					return;
				alive = true;
				inCombat = IsInCombat(ch);
				return;
			}
		}
		catch
		{
			alive = false;
			inCombat = false;
		}
	}

	private void ScanParty(
		IGameObject? player,
		bool playerInCombat,
		ref bool partyTargetsHuntMob,
		ref float? distanceToPartyHuntMob,
		ref bool anyAllyInCombat)
	{
		var localId = player != null ? TryEntityId(player) : null;
		var bestMobDist = float.PositiveInfinity;

		try
		{
			for (var i = 0; i < partyList.Length; i++)
			{
				var member = partyList[i];
				if (member == null)
					continue;

				IGameObject? obj = null;
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

				var isLocal = localId != null && TryEntityId(obj) == localId;
				var allyInCombat = !isLocal && obj is ICharacter ally && IsInCombat(ally);
				if (allyInCombat)
					anyAllyInCombat = true;

				if (!TryGetBattleNpcTarget(obj, out var mob) || mob == null || player == null)
					continue;

				// Verified engage: targeter must be in combat (blocks bare tab-target).
				var targeterInCombat = isLocal
					? playerInCombat
					: allyInCombat || (obj is ICharacter c && IsInCombat(c));
				if (!targeterInCombat)
					continue;

				partyTargetsHuntMob = true;
				var d = Vector3.Distance(player.Position, mob.Position);
				if (d < bestMobDist)
				{
					bestMobDist = d;
					distanceToPartyHuntMob = d;
				}
			}
		}
		catch
		{
			// soft-fail: leave flags as accumulated
		}

		// Solo / party list empty: local BattleNpc target only while player in combat.
		if (player != null
			&& playerInCombat
			&& TryGetBattleNpcTarget(player, out var localMob)
			&& localMob != null)
		{
			partyTargetsHuntMob = true;
			var d = Vector3.Distance(player.Position, localMob.Position);
			if (distanceToPartyHuntMob is not float existing || d < existing)
				distanceToPartyHuntMob = d;
		}
	}

	private bool IsPlayerDead(ICharacter? player)
	{
		try
		{
			if (condition[ConditionFlag.Unconscious])
				return true;
		}
		catch
		{
			// fall through to HP check
		}

		if (player == null)
			return false;

		try
		{
			return player.CurrentHp <= 0;
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

	/// <summary>
	/// BattleNpc currently targeted by <paramref name="source"/>.
	/// Assumption: any living BattleNpc counts as hunt-pull signal (no rank filter).
	/// </summary>
	private static bool TryGetBattleNpcTarget(IGameObject source, out IGameObject? battleNpc)
	{
		battleNpc = null;
		try
		{
			var target = source.TargetObject;
			if (target == null || !target.IsValid())
				return false;
			if (target.ObjectKind != ObjectKind.BattleNpc)
				return false;
			if (target is ICharacter { CurrentHp: <= 0 })
				return false;

			battleNpc = target;
			return true;
		}
		catch
		{
			battleNpc = null;
			return false;
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
			// 0 / E0000000 are non-entities — latching them makes latchedAlive always false.
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
