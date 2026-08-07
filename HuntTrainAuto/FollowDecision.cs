#nullable enable
using System;
using System.Numerics;

namespace HuntTrainAuto;

/// <summary>Framework tick outcome for AD <c>FollowHelper.FollowUpdate</c>.</summary>
public enum FollowTickKind
{
	/// <summary>Soft-wait: missing player/target, throttle, plugin off, or vnavmesh absent.</summary>
	Wait,

	/// <summary>Within follow distance — no repath (AD does nothing when close).</summary>
	IdleWithinRange,

	/// <summary>Distance ≥ follow distance — <c>Path.Stop</c> then <c>Path.MoveTo</c> (ground).</summary>
	Repath,

	/// <summary>Follow disabled — stop any active path.</summary>
	StopPath,
}

/// <summary>Result of <see cref="FollowDecision.DecideFollowTick"/>.</summary>
public readonly struct FollowTickResult
{
	public required FollowTickKind Kind { get; init; }

	/// <summary>True when path should be stopped (disable or before repath).</summary>
	public bool StopPath { get; init; }

	/// <summary>True when <c>Path.MoveTo([target], fly: false)</c> should run.</summary>
	public bool MoveToTarget { get; init; }
}

/// <summary>
/// Pure follow-loop decisions (AD <c>FollowHelper</c>).
/// Object / IPC / Framework wiring stays in <see cref="FollowHelper"/>.
/// </summary>
public static class FollowDecision
{
	/// <summary>AD <c>EzThrottler.Throttle("FollowUpdate", 50)</c>.</summary>
	public const int UpdateCooldownMs = 50;

	/// <summary>
	/// Default follow distance when caller omits / passes ≤ 0
	/// (<see cref="Configuration.PartyFollowDistance"/>).
	/// </summary>
	public const float DefaultFollowDistance = 3f;

	/// <summary>Party-stack follow is always ground (<c>canFly: false</c>).</summary>
	public const bool PreferCanFly = false;

	/// <summary>
	/// Resolve configured distance: ≤ 0 → <paramref name="defaultDistance"/> (clamped ≥ 0).
	/// </summary>
	public static float ResolveFollowDistance(float requested, float defaultDistance = DefaultFollowDistance)
	{
		if (requested > 0f)
			return requested;
		return defaultDistance > 0f ? defaultDistance : DefaultFollowDistance;
	}

	/// <summary>AD: repath when distance to target ≥ follow distance.</summary>
	public static bool ShouldRepath(float distanceToTarget, float followDistance)
		=> distanceToTarget >= followDistance;

	/// <summary>Euclidean distance helper.</summary>
	public static float Distance(Vector3 from, Vector3 to)
		=> Vector3.Distance(from, to);

	/// <summary>Whether FollowUpdate throttle allows progress.</summary>
	public static bool IsUpdateReady(long nextUpdateMs, long nowMs)
		=> nowMs >= nextUpdateMs;

	/// <summary>
	/// If ready, arms a new cooldown and returns true
	/// (AD <c>EzThrottler.Throttle("FollowUpdate", 50)</c>).
	/// </summary>
	public static bool TryFireUpdate(ref long nextUpdateMs, long nowMs, int cooldownMs = UpdateCooldownMs)
	{
		if (nowMs < nextUpdateMs)
			return false;

		nextUpdateMs = nowMs + Math.Max(0, cooldownMs);
		return true;
	}

	/// <summary>
	/// Whether the follow loop may run: follow enabled, plugin master on, player + target present, vnavmesh available.
	/// </summary>
	public static bool CanRunFollow(
		bool followEnabled,
		bool pluginEnabled,
		bool playerAvailable,
		bool hasTarget,
		bool vnavAvailable)
		=> followEnabled && pluginEnabled && playerAvailable && hasTarget && vnavAvailable;

	/// <summary>
	/// Transition when <see cref="FollowHelper.Enabled"/> becomes false (or Clear/Stop):
	/// always stop path.
	/// </summary>
	public static FollowTickResult DecideOnDisable()
		=> new()
		{
			Kind = FollowTickKind.StopPath,
			StopPath = true,
			MoveToTarget = false,
		};

	/// <summary>
	/// When the follow target object changes while follow may still be running,
	/// stop any path computed for the previous target (avoids IdleWithinRange
	/// continuing an obsolete route).
	/// </summary>
	public static bool ShouldInvalidatePathOnTargetChange(bool targetChanged)
		=> targetChanged;

	/// <summary>
	/// One FollowUpdate decision after Enabled / availability gates.
	/// Caller applies throttle via <see cref="TryFireUpdate"/> before calling when Kind would need work.
	/// </summary>
	public static FollowTickResult DecideFollowTick(
		bool followEnabled,
		bool pluginEnabled,
		bool playerAvailable,
		bool hasTarget,
		bool vnavAvailable,
		bool throttleReady,
		float distanceToTarget,
		float followDistance)
	{
		if (!followEnabled || !pluginEnabled)
			return DecideOnDisable();

		if (!playerAvailable || !hasTarget || !vnavAvailable || !throttleReady)
		{
			return new FollowTickResult
			{
				Kind = FollowTickKind.Wait,
				StopPath = false,
				MoveToTarget = false,
			};
		}

		if (!ShouldRepath(distanceToTarget, followDistance))
		{
			return new FollowTickResult
			{
				Kind = FollowTickKind.IdleWithinRange,
				StopPath = false,
				MoveToTarget = false,
			};
		}

		return new FollowTickResult
		{
			Kind = FollowTickKind.Repath,
			StopPath = true,
			MoveToTarget = true,
		};
	}
}
