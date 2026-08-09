#nullable enable

using System;
using System.Collections.Generic;
using HuntTrainAuto.Chat;

namespace HuntTrainAuto.HuntAlerts;

/// <summary>Outcome of best-effort HA → conductor auto-assign.</summary>
public enum HuntAlertsConductorAssignKind
{
	/// <summary>No conductor name found in message text.</summary>
	NoName = 0,

	/// <summary>Name already present in the conductor list (case-insensitive).</summary>
	AlreadyPresent = 1,

	/// <summary>Name was added via <see cref="ConductorList.TryAdd"/>.</summary>
	Added = 2,

	/// <summary>Auto-assign disabled by config — list unchanged.</summary>
	Disabled = 3,
}

/// <summary>
/// Pure assign decision for HuntAlerts train notifications.
/// Reuses <see cref="ConductorList"/> — same store as Phase 1 / context menu / Chat2.
/// </summary>
public static class HuntAlertsConductorDecision
{
	public readonly record struct Result(HuntAlertsConductorAssignKind Kind, string? Name, string? World);

	/// <summary>Compact, log-safe decision summary.</summary>
	public static string Describe(Result result)
		=> result.Kind switch
		{
			HuntAlertsConductorAssignKind.Added => result.World == null
				? $"added conductor '{result.Name}'"
				: $"added conductor '{result.Name}' @{result.World}",
			HuntAlertsConductorAssignKind.AlreadyPresent => $"conductor already present '{result.Name}'",
			HuntAlertsConductorAssignKind.Disabled => "auto-conductor disabled",
			_ => "no conductor name",
		};

	/// <summary>
	/// Parse <paramref name="message"/> and, on success, <see cref="ConductorList.TryAdd"/> the
	/// bare character name. Never throws for bad input; caller soft-fails around IPC.
	/// </summary>
	public static Result Decide(IList<string> conductors, string? message, bool enabled = true)
	{
		ArgumentNullException.ThrowIfNull(conductors);

		if (!enabled)
			return new Result(HuntAlertsConductorAssignKind.Disabled, null, null);

		if (!HuntAlertsConductorParse.TryExtract(message, out var name, out var world))
			return new Result(HuntAlertsConductorAssignKind.NoName, null, null);

		if (ConductorList.TryAdd(conductors, name))
			return new Result(HuntAlertsConductorAssignKind.Added, name, world);

		return new Result(HuntAlertsConductorAssignKind.AlreadyPresent, name, world);
	}
}
