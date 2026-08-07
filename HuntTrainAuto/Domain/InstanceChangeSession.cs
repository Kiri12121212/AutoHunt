#nullable enable

namespace HuntTrainAuto.Domain;

/// <summary>Framework phases for post-TP instance switching.</summary>
public enum InstanceChangePhase
{
	Idle,
	/// <summary>Wait territory match + interactable + BetweenAreas clear.</summary>
	WaitLanded,
	/// <summary>Screen ready; approach aetheryte until <c>CanChangeInstance</c>.</summary>
	Approach,
	/// <summary>Issue <c>ChangeInstance</c> and wait until current == target (15s).</summary>
	Changing,
}

/// <summary>
/// Active Framework instance-change job (survives <see cref="TeleportPlan"/> clear).
/// </summary>
public sealed class InstanceChangeSession
{
	public InstanceChangePhase Phase { get; private set; }

	public bool IsActive => Phase != InstanceChangePhase.Idle;

	public int Instance { get; private set; }

	public uint Territory { get; private set; }

	/// <summary>Deadline for the Changing phase (<see cref="Environment.TickCount64"/>).</summary>
	public long ChangeDeadlineMs { get; private set; }

	public bool AutomoveStarted { get; set; }

	public bool ChangeIssued { get; set; }

	public long NextTargetMs { get; set; }

	public long NextLockonMs { get; set; }

	/// <summary>Start or replace a pending instance change.</summary>
	public void Enqueue(int instance, uint territory)
	{
		Instance = instance;
		Territory = territory;
		Phase = InstanceChangePhase.WaitLanded;
		ChangeDeadlineMs = 0;
		AutomoveStarted = false;
		ChangeIssued = false;
		NextTargetMs = 0;
		NextLockonMs = 0;
	}

	public void EnterApproach()
	{
		Phase = InstanceChangePhase.Approach;
	}

	public void EnterChanging(long nowMs)
	{
		Phase = InstanceChangePhase.Changing;
		ChangeDeadlineMs = nowMs + InstanceChangeDecision.ChangeTimeoutMs;
		ChangeIssued = false;
	}

	public void Clear()
	{
		Phase = InstanceChangePhase.Idle;
		Instance = 0;
		Territory = 0;
		ChangeDeadlineMs = 0;
		AutomoveStarted = false;
		ChangeIssued = false;
		NextTargetMs = 0;
		NextLockonMs = 0;
	}
}
