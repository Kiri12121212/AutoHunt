#nullable enable

namespace HuntTrainAuto.Tests.Movement;

public sealed class MeshPathfindRetryDecisionTests
{
	[Fact]
	public void Decide_waits_when_cannot_start()
		=> Assert.Equal(
			MeshPathfindRetryKind.WaitNotReady,
			MeshPathfindRetryDecision.Decide(
				canStartMeshPathfind: false,
				nowMs: 10_000,
				nextAttemptMs: 0,
				attempts: 0));

	[Fact]
	public void Decide_starts_when_ready_and_cooldown_elapsed()
		=> Assert.Equal(
			MeshPathfindRetryKind.Start,
			MeshPathfindRetryDecision.Decide(
				canStartMeshPathfind: true,
				nowMs: 5_000,
				nextAttemptMs: 4_000,
				attempts: 0));

	[Fact]
	public void Decide_waits_cooldown()
		=> Assert.Equal(
			MeshPathfindRetryKind.WaitCooldown,
			MeshPathfindRetryDecision.Decide(
				canStartMeshPathfind: true,
				nowMs: 5_000,
				nextAttemptMs: 6_000,
				attempts: 1));

	[Fact]
	public void Decide_exhausted_at_max_attempts()
		=> Assert.Equal(
			MeshPathfindRetryKind.Exhausted,
			MeshPathfindRetryDecision.Decide(
				canStartMeshPathfind: true,
				nowMs: 99_000,
				nextAttemptMs: 0,
				attempts: MeshPathfindRetryDecision.MaxAttempts));

	[Fact]
	public void AfterStartAttempt_arms_cooldown_and_bumps_count()
	{
		long next = 0;
		var attempts = 0;
		MeshPathfindRetryDecision.AfterStartAttempt(ref next, ref attempts, nowMs: 1_000);
		Assert.Equal(1, attempts);
		Assert.Equal(1_000 + MeshPathfindRetryDecision.RetryCooldownMs, next);
	}

	[Fact]
	public void Reset_clears_bookkeeping()
	{
		long next = 9_000;
		var attempts = 7;
		MeshPathfindRetryDecision.Reset(ref next, ref attempts);
		Assert.Equal(0, next);
		Assert.Equal(0, attempts);
	}

	[Theory]
	[InlineData(false, 0, false)]
	[InlineData(true, 0, true)]
	[InlineData(false, 3, true)]
	[InlineData(true, 2, true)]
	public void ShouldResetOnNavProgress(bool pathRunning, int waypoints, bool expected)
		=> Assert.Equal(
			expected,
			MeshPathfindRetryDecision.ShouldResetOnNavProgress(pathRunning, waypoints));

	[Theory]
	[InlineData(true, true)]
	[InlineData(false, false)]
	public void ShouldCountStartAttempt(bool onMesh, bool expected)
		=> Assert.Equal(expected, MeshPathfindRetryDecision.ShouldCountStartAttempt(onMesh));

	[Theory]
	[InlineData(false, true, true)]
	[InlineData(true, true, false)]
	[InlineData(false, false, false)]
	[InlineData(true, false, false)]
	public void ShouldResetOnMeshAcquire(bool wasOn, bool nowOn, bool expected)
		=> Assert.Equal(
			expected,
			MeshPathfindRetryDecision.ShouldResetOnMeshAcquire(wasOn, nowOn));

	[Theory]
	[InlineData(false, true, false, true)] // ground on-mesh
	[InlineData(false, false, false, false)] // ground off-mesh
	[InlineData(true, false, false, false)] // fly off-mesh, no prior nav → wait (0dv8)
	[InlineData(true, false, true, true)] // fly off-mesh after prior nav → repath (8sy1)
	[InlineData(true, true, false, true)] // fly on-mesh
	public void CanStartPathfindOffMeshPolicy(
		bool fly,
		bool onMesh,
		bool hadProgress,
		bool expected)
		=> Assert.Equal(
			expected,
			MeshPathfindRetryDecision.CanStartPathfindOffMeshPolicy(fly, onMesh, hadProgress));
}
