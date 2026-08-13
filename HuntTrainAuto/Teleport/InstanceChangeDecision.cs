#nullable enable

namespace HuntTrainAuto.Teleport;

/// <summary>
/// Pure post-teleport instance-switch decisions (HTA <c>TaskChangeInstanceAfterTeleport</c>).
/// Game object / IPC wiring stays in the Framework session.
/// </summary>
public static class InstanceChangeDecision
{
	/// <summary>
	/// Wait for <c>ChangeInstance</c> / current-instance match.
	/// Must exceed Lifestream <c>InteractWithAetheryte</c> (30s) + BetweenAreas load.
	/// </summary>
	public const int ChangeTimeoutMs = 45_000;

	/// <summary>
	/// Min gap between <c>ChangeInstance</c> IPC calls when a prior issue failed
	/// (Lifestream NRE / aborted) while still in range.
	/// </summary>
	public const int ChangeReissueMs = 3_000;

	/// <summary>Throttle for setting aetheryte target (EzThrottler-style).</summary>
	public const int TargetThrottleMs = 200;

	/// <summary>Throttle for <c>/lockon</c> + <c>/automove on</c>.</summary>
	public const int LockonThrottleMs = 200;

	/// <summary>Whether a requested instance should be enqueued after teleport.</summary>
	public static bool ShouldEnqueue(int requestedInstance)
		=> requestedInstance > 0;

	/// <summary>
	/// Enqueue only when a positive target differs from the live current instance.
	/// Avoids no-op jobs when the plan already matches (wrong-target bugs still log upstream).
	/// </summary>
	public static bool ShouldEnqueueIfNeeded(int requestedInstance, int currentInstance)
		=> NeedsInstanceChange(requestedInstance, currentInstance, numberOfInstances: 0);

	/// <summary>True when already on the requested instance.</summary>
	public static bool IsAlreadyOnInstance(int requestedInstance, int currentInstance)
		=> requestedInstance > 0 && currentInstance == requestedInstance;

	/// <summary>
	/// Merge live PublicInstance / Lifestream / ClientState into a decision-facing instance.
	/// Unsplit public areas report <c>InstanceId == 0</c>; conductors/HA still label that as
	/// instance 1 — coerce 0 → 1 so same-instance compares match.
	/// </summary>
	public static int ResolveCurrentInstance(
		int publicInstanceId,
		int lifestreamInstance,
		int clientStateInstance)
	{
		var raw = publicInstanceId > 0
			? publicInstanceId
			: lifestreamInstance > 0
				? lifestreamInstance
				: clientStateInstance > 0
					? clientStateInstance
					: 0;
		return raw > 0 ? raw : 1;
	}

	/// <summary>Raw merge without unsplit→1 coerce (tests / diagnostics).</summary>
	public static int ResolveCurrentInstanceRaw(
		int publicInstanceId,
		int lifestreamInstance,
		int clientStateInstance)
		=> publicInstanceId > 0
			? publicInstanceId
			: lifestreamInstance > 0
				? lifestreamInstance
				: clientStateInstance > 0
					? clientStateInstance
					: 0;

	/// <summary>
	/// Whether to keep pursuing an instance change after land.
	/// Requires a known current instance (non-zero) that differs from the request.
	/// <paramref name="numberOfInstances"/> 0 = Lifestream cache unknown (still try when
	/// current differs); positive = known max — skip when request is above it.
	/// </summary>
	public static bool NeedsInstanceChange(int requestedInstance, int currentInstance, int numberOfInstances)
		=> requestedInstance > 0
			&& currentInstance > 0
			&& currentInstance != requestedInstance
			&& (numberOfInstances == 0 || requestedInstance <= numberOfInstances);

	/// <summary>
	/// Same-zone instance switch: Lifestream <c>CanChangeInstance</c> requires ~11y of an
	/// aetheryte. When false, HTA Sonar/HA parity is aetheryte TP first, then post-land
	/// <c>ChangeInstance</c>. Direct when already usable or no aetheryte id to TP to.
	/// </summary>
	public static bool ShouldAetheryteTeleportForInstanceSwitch(
		bool canChangeInstance,
		uint aetheryteId)
		=> !canChangeInstance && aetheryteId > 0;

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

	/// <summary>Compact, side-effect-free approach diagnostic for call-site logging.</summary>
	public static string Describe(ApproachAction action)
		=> $"approach={action}";

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

	/// <summary>Compact, side-effect-free change-tick diagnostic for call-site logging.</summary>
	public static string Describe(ChangeTickResult result)
		=> $"change={result}";

	/// <summary>
	/// Tick the change-instance wait. Prefer succeed on current==num.
	/// Re-issue when Lifestream is idle again after a failed/aborted invoke
	/// (<paramref name="reissueReady"/> — caller throttles via <see cref="ChangeReissueMs"/>).
	/// </summary>
	public static ChangeTickResult DecideChangeTick(
		bool alreadyOnInstance,
		bool canChangeInstance,
		bool changeIssued,
		bool timedOut,
		bool reissueReady = false)
	{
		if (alreadyOnInstance)
			return ChangeTickResult.Succeeded;

		if (timedOut)
			return ChangeTickResult.TimedOut;

		if (canChangeInstance && (!changeIssued || reissueReady))
			return ChangeTickResult.IssueChange;

		return ChangeTickResult.Continue;
	}

}
