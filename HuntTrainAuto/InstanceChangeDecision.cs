#nullable enable

namespace HuntTrainAuto;

/// <summary>
/// Pure post-teleport instance-switch decisions (HTA <c>TaskChangeInstanceAfterTeleport</c>).
/// Game object / IPC wiring stays in the Framework session.
/// </summary>
public static class InstanceChangeDecision
{
	/// <summary>HTA timed wait for <c>ChangeInstance</c> / current-instance match.</summary>
	public const int ChangeTimeoutMs = 15_000;

	/// <summary>Throttle for setting aetheryte target (EzThrottler-style).</summary>
	public const int TargetThrottleMs = 200;

	/// <summary>Throttle for <c>/lockon</c> + <c>/automove on</c>.</summary>
	public const int LockonThrottleMs = 200;

	/// <summary>Whether a requested instance should be enqueued after teleport.</summary>
	public static bool ShouldEnqueue(int requestedInstance)
		=> requestedInstance > 0;

	/// <summary>True when already on the requested instance.</summary>
	public static bool IsAlreadyOnInstance(int requestedInstance, int currentInstance)
		=> requestedInstance > 0 && currentInstance == requestedInstance;

	/// <summary>
	/// HTA: skip when <c>instances==0 || num==0 || current==num</c>.
	/// </summary>
	public static bool NeedsInstanceChange(int requestedInstance, int currentInstance, int numberOfInstances)
		=> numberOfInstances != 0
			&& requestedInstance != 0
			&& currentInstance != requestedInstance;

	/// <summary>
	/// Landed after TP: target territory, player usable, not mid BetweenAreas.
	/// </summary>
	public static bool IsLanded(bool territoryMatches, bool playerReady, bool betweenAreas)
		=> territoryMatches && playerReady && !betweenAreas;

	/// <summary>Approach / readiness while waiting for <c>CanChangeInstance</c>.</summary>
	public enum ApproachAction
	{
		Wait,
		SetTarget,
		LockonAndAutomove,
		SoftAbortNoAetheryte,
		ReadyToChange,
	}

	/// <summary>
	/// Decide aetheryte approach step when instance change is still needed.
	/// </summary>
	public static ApproachAction DecideApproach(
		bool canChangeInstance,
		bool hasNearestAetheryte,
		bool aetheryteIsTargeted,
		bool targetThrottleReady,
		bool lockonThrottleReady)
	{
		if (canChangeInstance)
			return ApproachAction.ReadyToChange;

		if (!hasNearestAetheryte)
			return ApproachAction.SoftAbortNoAetheryte;

		if (!aetheryteIsTargeted)
			return targetThrottleReady ? ApproachAction.SetTarget : ApproachAction.Wait;

		return lockonThrottleReady ? ApproachAction.LockonAndAutomove : ApproachAction.Wait;
	}

	/// <summary>Outcome while issuing / awaiting instance change.</summary>
	public enum ChangeTickResult
	{
		Continue,
		Succeeded,
		TimedOut,
		IssueChange,
	}

	/// <summary>
	/// Tick the change-instance wait (15s HTA parity). Prefer succeed on current==num.
	/// </summary>
	public static ChangeTickResult DecideChangeTick(
		bool alreadyOnInstance,
		bool canChangeInstance,
		bool changeIssued,
		bool timedOut)
	{
		if (alreadyOnInstance)
			return ChangeTickResult.Succeeded;

		if (timedOut)
			return ChangeTickResult.TimedOut;

		if (!changeIssued && canChangeInstance)
			return ChangeTickResult.IssueChange;

		return ChangeTickResult.Continue;
	}
}
