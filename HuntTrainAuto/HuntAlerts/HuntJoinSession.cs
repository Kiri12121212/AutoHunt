#nullable enable

namespace HuntTrainAuto.HuntAlerts;

/// <summary>Active go-to-hunt + find-conductor job.</summary>
public sealed class HuntJoinSession
{
	public HuntJoinPhase Phase { get; private set; }

	public bool IsActive => Phase != HuntJoinPhase.Idle;

	public HuntJoinDecision.Plan Plan { get; private set; }

	public long DeadlineMs { get; private set; }

	public long NextRetryMs { get; private set; }

	public string Status { get; set; } = "";

	public void Start(HuntJoinDecision.Plan plan, HuntJoinPhase phase, long nowMs)
	{
		Plan = plan;
		Phase = phase;
		DeadlineMs = nowMs + HuntJoinDecision.OverallTimeoutMs;
		NextRetryMs = 0;
		Status = $"joining {HuntJoinDecision.Describe(plan)}";
	}

	public void SetPhase(HuntJoinPhase phase, long nowMs, bool retryNow = true)
	{
		Phase = phase;
		if (retryNow)
			NextRetryMs = nowMs;
	}

	public void MarkRetry(long nowMs)
		=> NextRetryMs = nowMs + HuntJoinDecision.RetryIntervalMs;

	public bool IsRetryReady(long nowMs)
		=> nowMs >= NextRetryMs;

	public bool IsTimedOut(long nowMs)
		=> nowMs >= DeadlineMs;

	public void Clear()
	{
		Phase = HuntJoinPhase.Idle;
		Plan = default;
		DeadlineMs = 0;
		NextRetryMs = 0;
	}
}
