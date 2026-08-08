#nullable enable
using System;

namespace HuntTrainAuto.Combat;

/// <summary>
/// Pure RSR HostileType / targeting resolution (TASKS 6.3–6.4).
/// No Dalamud types — job role is a ClassJob.Role byte.
/// </summary>
public static class RsrSettingsDecision
{
	/// <summary>Lumina <c>ClassJob.Role</c> value for tanks.</summary>
	public const byte TankClassJobRole = 1;

	/// <summary>Lumina <c>ClassJob.Role</c> value for melee DPS.</summary>
	public const byte MeleeDpsClassJobRole = 2;

	/// <summary>AD default: <see cref="RsrTargetHostileType.AllTargetsCanAttack"/>.</summary>
	public static RsrTargetHostileType DefaultHostileType => RsrTargetHostileType.AllTargetsCanAttack;

	/// <summary>AD default tank targeting: <see cref="RsrTargetingType.HighHP"/>.</summary>
	public static RsrTargetingType DefaultTankTargeting => RsrTargetingType.HighHP;

	/// <summary>AD default non-tank targeting: <see cref="RsrTargetingType.LowHP"/>.</summary>
	public static RsrTargetingType DefaultNonTankTargeting => RsrTargetingType.LowHP;

	/// <summary>True when <paramref name="classJobRole"/> is tank (<see cref="TankClassJobRole"/>).</summary>
	public static bool IsTankRole(byte classJobRole) => classJobRole == TankClassJobRole;

	/// <summary>True when <paramref name="classJobRole"/> is melee DPS (<see cref="MeleeDpsClassJobRole"/>).</summary>
	public static bool IsMeleeDpsRole(byte classJobRole) => classJobRole == MeleeDpsClassJobRole;

	/// <summary>
	/// Tank or melee DPS — need melee engage distance (RSR will not walk in).
	/// </summary>
	public static bool IsMeleeEngageRole(byte classJobRole)
		=> IsTankRole(classJobRole) || IsMeleeDpsRole(classJobRole);

	/// <summary>
	/// Clamp to a defined <see cref="RsrTargetHostileType"/>;
	/// undefined → <see cref="DefaultHostileType"/>.
	/// </summary>
	public static RsrTargetHostileType ClampHostileType(RsrTargetHostileType hostileType)
		=> Enum.IsDefined(hostileType) ? hostileType : DefaultHostileType;

	/// <summary>
	/// Clamp to a defined <see cref="RsrTargetingType"/>;
	/// undefined → <paramref name="fallback"/>.
	/// </summary>
	public static RsrTargetingType ClampTargetingType(
		RsrTargetingType targeting,
		RsrTargetingType fallback)
		=> Enum.IsDefined(targeting) ? targeting : fallback;

	/// <summary>
	/// Role-based targeting from config: tank → clamped tank setting,
	/// otherwise clamped non-tank setting.
	/// </summary>
	public static RsrTargetingType ResolveTargeting(
		bool isTank,
		RsrTargetingType tankTargeting,
		RsrTargetingType nonTankTargeting)
		=> isTank
			? ClampTargetingType(tankTargeting, DefaultTankTargeting)
			: ClampTargetingType(nonTankTargeting, DefaultNonTankTargeting);

	/// <summary>
	/// Full RotationAuto args from role + persisted config
	/// (hostile + tank / non-tank targeting).
	/// </summary>
	public static (RsrTargetingType Targeting, RsrTargetHostileType Hostile) Resolve(
		bool isTank,
		RsrTargetHostileType hostileType,
		RsrTargetingType tankTargeting,
		RsrTargetingType nonTankTargeting)
		=> (
			ResolveTargeting(isTank, tankTargeting, nonTankTargeting),
			ClampHostileType(hostileType));
}
