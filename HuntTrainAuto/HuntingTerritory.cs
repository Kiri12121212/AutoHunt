using System;
using System.Collections.Generic;

namespace HuntTrainAuto;

/// <summary>
/// HTA Utils.IsInHuntingTerritory — open-world intended use, special hubs, Idyllshire.
/// Intended-use lookup is injected so unit tests need no Lumina.
/// </summary>
public static class HuntingTerritory
{
	public const uint OpenWorldIntendedUse = 1;
	public const uint Idyllshire = 478;

	// Mare gateway, Doman Enclave, Rhalgr's Reach, related hubs.
	private static readonly HashSet<uint> SpecialTerritories =
	[
		1024,
		682,
		739,
		759,
		635,
		659,
	];

	public static bool IsHuntingTerritory(uint territoryTypeId, Func<uint, uint?> getIntendedUseRowId)
	{
		ArgumentNullException.ThrowIfNull(getIntendedUseRowId);

		if (SpecialTerritories.Contains(territoryTypeId) || territoryTypeId == Idyllshire)
			return true;

		var intendedUse = getIntendedUseRowId(territoryTypeId);
		// Fail-open when Excel data is unavailable — avoid clearing conductors mid-hunt.
		if (intendedUse == null)
			return true;

		return intendedUse == OpenWorldIntendedUse;
	}
}
