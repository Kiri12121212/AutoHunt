#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;

namespace HuntTrainAuto;

/// <summary>
/// Thin game wiring for follow-target priority (TASKS 5.2–5.5).
/// Builds candidate snapshots → <see cref="FollowTargetDecision"/> →
/// <see cref="FollowHelper.SetFollow"/> / clear on soft-fail.
/// </summary>
public sealed class FollowTargetResolver
{
	private readonly IObjectTable objectTable;
	private readonly IPartyList partyList;
	private readonly IPluginLog pluginLog;
	private readonly List<FollowTargetCandidate> candidates = [];
	private readonly List<IGameObject> candidateObjects = [];

	public FollowTargetResolver(
		IObjectTable objectTable,
		IPartyList partyList,
		IPluginLog pluginLog)
	{
		this.objectTable = objectTable;
		this.partyList = partyList;
		this.pluginLog = pluginLog;
	}

	/// <summary>Last resolution kind (debug / status); <see cref="FollowTargetKind.None"/> if soft-fail.</summary>
	public FollowTargetKind LastKind { get; private set; } = FollowTargetKind.None;

	/// <summary>
	/// Resolve conductor → party leader → in-combat ally and apply to <paramref name="follow"/>.
	/// Soft-fails by clearing follow when nothing is found.
	/// Only calls SetFollow when the resolved <see cref="IGameObject.EntityId"/> changes
	/// (avoids path invalidation from wrapper churn).
	/// </summary>
	public void ResolveAndApply(
		FollowHelper follow,
		IList<string> conductors,
		bool followConductorFirst)
	{
		try
		{
			ResolveAndApplyCore(follow, conductors, followConductorFirst);
		}
		catch (Exception ex)
		{
			pluginLog.Debug($"FollowTargetResolver soft-fail: {ex.Message}");
			try
			{
				Apply(follow, null, FollowTargetKind.None);
			}
			catch (Exception clearEx)
			{
				pluginLog.Debug($"FollowTargetResolver clear soft-fail: {clearEx.Message}");
			}
		}
	}

	private void ResolveAndApplyCore(
		FollowHelper follow,
		IList<string> conductors,
		bool followConductorFirst)
	{
		var player = objectTable.LocalPlayer;
		if (player == null)
		{
			Apply(follow, null, FollowTargetKind.None);
			return;
		}

		BuildCandidates(player, conductors);
		var pick = FollowTargetDecision.Resolve(candidates, followConductorFirst);
		if (!pick.Found || pick.Index < 0 || pick.Index >= candidateObjects.Count)
		{
			Apply(follow, null, FollowTargetKind.None);
			return;
		}

		Apply(follow, candidateObjects[pick.Index], pick.Kind);
	}

	private void BuildCandidates(IPlayerCharacter localPlayer, IList<string> conductors)
	{
		candidates.Clear();
		candidateObjects.Clear();

		var localId = localPlayer.EntityId;
		var leaderId = TryGetPartyLeaderEntityId();
		var partyIds = CollectPartyEntityIds();
		var playerPos = localPlayer.Position;

		foreach (var obj in objectTable)
		{
			if (obj is not IPlayerCharacter pc)
				continue;

			if (!TryIsValid(pc))
				continue;

			var entityId = pc.EntityId;
			var isLocal = entityId == localId;
			var name = GetName(pc);
			var isConductor = !isLocal && ChatSender.IsConductor(conductors, name);
			var isLeader = !isLocal && leaderId != null && entityId == leaderId.Value;
			// In-combat fallback is party allies only (not random nearby players).
			var inParty = !isLocal && partyIds.Contains(entityId);
			var inCombat = inParty && IsInCombat(pc);
			var distance = Vector3.Distance(playerPos, pc.Position);

			var index = candidateObjects.Count;
			candidateObjects.Add(pc);
			candidates.Add(new FollowTargetCandidate
			{
				Index = index,
				Name = name,
				IsConductor = isConductor,
				IsLeader = isLeader,
				InCombat = inCombat,
				Distance = distance,
				IsLocalPlayer = isLocal,
			});
		}
	}

	private uint? TryGetPartyLeaderEntityId()
	{
		try
		{
			if (partyList.Length <= 0)
				return null;

			var index = (int)partyList.PartyLeaderIndex;
			var leader = partyList[index];
			if (leader == null)
				return null;

			return leader.EntityId;
		}
		catch
		{
			return null;
		}
	}

	private HashSet<uint> CollectPartyEntityIds()
	{
		var ids = new HashSet<uint>();
		try
		{
			for (var i = 0; i < partyList.Length; i++)
			{
				var member = partyList[i];
				if (member == null)
					continue;
				ids.Add(member.EntityId);
			}
		}
		catch
		{
			// soft-fail: empty set → no in-combat party fallback
		}

		return ids;
	}

	private void Apply(FollowHelper follow, IGameObject? target, FollowTargetKind kind)
	{
		LastKind = kind;

		if (target == null)
		{
			if (follow.Enabled || follow.FollowTarget != null)
				follow.SetFollow(null);
			return;
		}

		var current = follow.FollowTarget;
		if (follow.Enabled
			&& current != null
			&& TryEntityId(current) is { } currentId
			&& currentId == target.EntityId)
		{
			return;
		}

		follow.SetFollow(target);
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

	private static uint? TryEntityId(IGameObject obj)
	{
		try
		{
			if (!obj.IsValid())
				return null;
			return obj.EntityId;
		}
		catch
		{
			return null;
		}
	}
}
