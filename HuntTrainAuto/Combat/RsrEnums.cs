#nullable enable

namespace HuntTrainAuto.Combat;

/// <summary>
/// Mirror of RSR <c>StateCommandType</c> (<c>: byte</c>) for CallGate args.
/// Values must match FFXIV-CombatReborn/RotationSolverReborn <c>RSCommandType.cs</c>.
/// </summary>
public enum RsrStateCommandType : byte
{
	Off = 0,
	Auto = 1,
	TargetOnly = 2,
	Manual = 3,
	AutoDuty = 4,
	Henched = 5,
	PvP = 6,
}

/// <summary>
/// Mirror of RSR <c>OtherCommandType</c> (<c>: byte</c>) for CallGate args.
/// </summary>
public enum RsrOtherCommandType : byte
{
	Settings = 0,
	Rotations = 1,
	DutyRotations = 2,
	DoActions = 3,
	ToggleActions = 4,
	NextAction = 5,
	Cycle = 6,
}

/// <summary>
/// Mirror of RSR <c>TargetingType</c> (underlying <c>int</c>) for CallGate args.
/// Values must match <c>RotationSolver.Basic/Data/TargetType.cs</c>.
/// </summary>
public enum RsrTargetingType
{
	Big = 0,
	Small = 1,
	HighHP = 2,
	LowHP = 3,
	HighHPPercent = 4,
	LowHPPercent = 5,
	HighMaxHP = 6,
	LowMaxHP = 7,
	Nearest = 8,
	Farthest = 9,
	PvPHealers = 10,
	PvPTanks = 11,
	PvPDPS = 12,
}

/// <summary>
/// Mirror of RSR <c>TargetHostileType</c> (<c>: byte</c>).
/// Passed to RSR as a Settings string (not an IPC enum arg).
/// </summary>
public enum RsrTargetHostileType : byte
{
	AllTargetsCanAttack = 0,
	TargetsHaveTarget = 1,
	AllTargetsWhenSoloInDuty = 2,
	AllTargetsWhenSolo = 3,
	SoloDeepDungeonSmart = 4,
}
