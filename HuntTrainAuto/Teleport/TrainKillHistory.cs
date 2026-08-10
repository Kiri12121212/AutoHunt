#nullable enable
using System.Collections.Generic;
using System.Numerics;
using HuntTrainAuto.Map;

namespace HuntTrainAuto.Teleport;

/// <summary>
/// One completed A-rank combat on a hunt train (territory + flag raw coords).
/// </summary>
public readonly struct TrainKillRecord
{
	public required uint TerritoryTypeId { get; init; }

	public required int RawX { get; init; }

	public required int RawY { get; init; }

	/// <summary>Player instance at kill time (0 = unknown).</summary>
	public int InstanceAtKill { get; init; }
}

/// <summary>
/// Session kill list for instance-swap heuristics (kata 6cdc).
/// Counts kills, not flags. Cleared on full train leave.
/// Kill signals are scoped to the instance they happened on so a successful
/// swap does not immediately toggle back on the next flag.
/// </summary>
public sealed class TrainKillHistory
{
	private readonly List<TrainKillRecord> kills = [];
	private readonly Dictionary<uint, int> maxInstancesByTerritory = new();

	/// <summary>Reuse map-open / HA near-dupe threshold (scaled map units).</summary>
	public const float NearKillDistanceThreshold = MapOpenDedupe.DuplicateDistanceThreshold;

	public int TotalCount => kills.Count;

	public void Clear()
	{
		kills.Clear();
		maxInstancesByTerritory.Clear();
	}

	/// <summary>Record a combat-end kill for the active flag's territory/coords.</summary>
	public void Record(uint territoryTypeId, int rawX, int rawY, int instanceAtKill = 0)
	{
		kills.Add(new TrainKillRecord
		{
			TerritoryTypeId = territoryTypeId,
			RawX = rawX,
			RawY = rawY,
			InstanceAtKill = instanceAtKill > 0 ? instanceAtKill : 0,
		});
	}

	/// <summary>
	/// Remember Lifestream instance count when ≥ 2 so a later 0 (cache miss) can still
	/// unlock heuristics on the same territory.
	/// </summary>
	public void NoteInstanceCount(uint territoryTypeId, int numberOfInstances)
	{
		if (numberOfInstances < 2)
			return;

		if (!maxInstancesByTerritory.TryGetValue(territoryTypeId, out var prev)
		    || numberOfInstances > prev)
			maxInstancesByTerritory[territoryTypeId] = numberOfInstances;
	}

	public int RememberedMaxInstances(uint territoryTypeId)
		=> maxInstancesByTerritory.TryGetValue(territoryTypeId, out var n) ? n : 0;

	public int CountForTerritory(uint territoryTypeId)
	{
		var n = 0;
		foreach (var k in kills)
		{
			if (k.TerritoryTypeId == territoryTypeId)
				n++;
		}

		return n;
	}

	/// <summary>
	/// Kills on this territory that belong to <paramref name="currentInstance"/>
	/// (unknown InstanceAtKill=0 counts for any current &gt; 0).
	/// </summary>
	public int CountForTerritoryOnInstance(uint territoryTypeId, int currentInstance)
	{
		if (currentInstance <= 0)
			return 0;

		var n = 0;
		foreach (var k in kills)
		{
			if (k.TerritoryTypeId != territoryTypeId)
				continue;
			if (k.InstanceAtKill > 0 && k.InstanceAtKill != currentInstance)
				continue;
			n++;
		}

		return n;
	}

	/// <summary>
	/// True when <paramref name="rawX"/>/<paramref name="rawY"/> is within
	/// <paramref name="distanceThreshold"/> of any prior kill on the same territory.
	/// </summary>
	public bool IsNearPriorKill(
		uint territoryTypeId,
		int rawX,
		int rawY,
		float distanceThreshold = NearKillDistanceThreshold)
	{
		var incoming = MapOpenDedupe.LinkPosFromRaw(rawX, rawY);
		foreach (var k in kills)
		{
			if (k.TerritoryTypeId != territoryTypeId)
				continue;

			var prior = MapOpenDedupe.LinkPosFromRaw(k.RawX, k.RawY);
			if (Vector2.Distance(prior, incoming) <= distanceThreshold)
				return true;
		}

		return false;
	}

	/// <summary>
	/// Near a prior kill that happened on <paramref name="currentInstance"/>
	/// (or unknown instance). Kills from other instances do not count — avoids
	/// toggling back after a successful swap when the conductor re-marks the same A.
	/// </summary>
	public bool IsNearPriorKillOnInstance(
		uint territoryTypeId,
		int rawX,
		int rawY,
		int currentInstance,
		float distanceThreshold = NearKillDistanceThreshold)
	{
		if (currentInstance <= 0)
			return false;

		var incoming = MapOpenDedupe.LinkPosFromRaw(rawX, rawY);
		foreach (var k in kills)
		{
			if (k.TerritoryTypeId != territoryTypeId)
				continue;
			if (k.InstanceAtKill > 0 && k.InstanceAtKill != currentInstance)
				continue;

			var prior = MapOpenDedupe.LinkPosFromRaw(k.RawX, k.RawY);
			if (Vector2.Distance(prior, incoming) <= distanceThreshold)
				return true;
		}

		return false;
	}

	/// <summary>
	/// Incoming flag should bypass near-dupe suppress so instance-swap heuristics can run
	/// (3rd marker after 2 kills on current instance, or re-flag of an A killed on current).
	/// </summary>
	public bool SuggestsInstanceSwapReflag(
		uint territoryTypeId,
		int rawX,
		int rawY,
		int currentInstance)
		=> CountForTerritoryOnInstance(territoryTypeId, currentInstance) >= 2
		   || IsNearPriorKillOnInstance(territoryTypeId, rawX, rawY, currentInstance);

	public string DescribeTerritory(uint territoryTypeId, int currentInstance = 0)
	{
		var onInst = currentInstance > 0
			? $" killsOnCurrent={CountForTerritoryOnInstance(territoryTypeId, currentInstance)}"
			: string.Empty;
		return $"kills={CountForTerritory(territoryTypeId)}/{TotalCount} territory={territoryTypeId}"
			+ $" rememberedInstances={RememberedMaxInstances(territoryTypeId)}{onInst}";
	}
}
