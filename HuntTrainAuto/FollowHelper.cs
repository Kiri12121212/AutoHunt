#nullable enable
using System;
using System.Numerics;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using HuntTrainAuto.Services;

namespace HuntTrainAuto;

/// <summary>
/// Framework-tick port of AutoDuty <c>FollowHelper</c> (vanilla Dalamud, no ECommons).
/// Soft-fails; never throws to Framework. Target resolution is owned by phase 5.2+.
/// </summary>
public sealed class FollowHelper
{
	private readonly VNavmeshIpc vnav;
	private readonly IObjectTable objectTable;
	private readonly IPluginLog pluginLog;
	private readonly Func<float> getDefaultFollowDistance;

	private IGameObject? followTarget;
	private float followDistance;
	private bool followDistanceOverride;
	private bool enabled;
	private long nextUpdateMs;

	public FollowHelper(
		VNavmeshIpc vnav,
		IObjectTable objectTable,
		IPluginLog pluginLog,
		Func<float> getDefaultFollowDistance)
	{
		this.vnav = vnav;
		this.objectTable = objectTable;
		this.pluginLog = pluginLog;
		this.getDefaultFollowDistance = getDefaultFollowDistance;
		followDistance = FollowDecision.ResolveFollowDistance(0f, getDefaultFollowDistance());
	}

	/// <summary>Current follow target (may be null / invalid — soft-wait).</summary>
	public IGameObject? FollowTarget => followTarget;

	/// <summary>Effective follow distance (yalms).</summary>
	public float FollowDistance => followDistance;

	/// <summary>
	/// AD <c>FollowHelper.Enabled</c>: when set false, stops path.
	/// Framework tick is driven by <see cref="Plugin"/> (no self-hook).
	/// </summary>
	public bool Enabled
	{
		get => enabled;
		set
		{
			if (enabled == value)
				return;

			enabled = value;
			if (!value)
			{
				ApplyDisable();
			}
		}
	}

	/// <summary>
	/// AD <c>SetFollow</c>: non-null enables; null disables.
	/// <paramref name="distance"/> ≤ 0 keeps config-backed distance (live-refreshed).
	/// <paramref name="distance"/> &gt; 0 sets an override until Clear or another SetFollowDistance.
	/// Target swap invalidates any active path for the previous target.
	/// </summary>
	public void SetFollow(IGameObject? gameObject, float distance = 0f)
	{
		if (gameObject != null)
		{
			InvalidatePathIfTargetChanged(gameObject);
			followTarget = gameObject;
			if (distance > 0f)
			{
				followDistance = FollowDecision.ResolveFollowDistance(distance, getDefaultFollowDistance());
				followDistanceOverride = true;
			}
			Enabled = true;
		}
		else
		{
			followTarget = null;
			followDistanceOverride = false;
			Enabled = false;
		}
	}

	/// <summary>
	/// AD <c>SetFollowTarget</c> — does not change <see cref="Enabled"/>.
	/// Target swap invalidates any active path for the previous target.
	/// </summary>
	public void SetFollowTarget(IGameObject? gameObject)
	{
		InvalidatePathIfTargetChanged(gameObject);
		followTarget = gameObject;
	}

	/// <summary>
	/// AD <c>SetFollowDistance</c> — sticky override until Clear / SetFollow(null).
	/// Config live-refresh is skipped while override is active.
	/// </summary>
	public void SetFollowDistance(float distance)
	{
		followDistance = FollowDecision.ResolveFollowDistance(distance, getDefaultFollowDistance());
		followDistanceOverride = true;
	}

	/// <summary>Disable follow and stop path (new flag / leave territory / dispose).</summary>
	public void Clear() => Stop();

	/// <summary>Alias for <see cref="Clear"/>.</summary>
	public void Stop()
	{
		followTarget = null;
		followDistanceOverride = false;
		Enabled = false;
	}

	/// <summary>
	/// One Framework tick. No-op when disabled.
	/// When <paramref name="pluginEnabled"/> is false, stops follow (master toggle).
	/// </summary>
	public void Tick(bool pluginEnabled)
	{
		try
		{
			TickCore(pluginEnabled);
		}
		catch (Exception ex)
		{
			pluginLog.Debug($"FollowHelper soft-fail: {ex.Message}");
		}
	}

	private void TickCore(bool pluginEnabled)
	{
		if (!pluginEnabled)
		{
			if (enabled)
				Enabled = false;
			return;
		}

		if (!enabled)
			return;

		var now = Environment.TickCount64;
		var player = objectTable.LocalPlayer;
		var playerAvailable = player != null;
		var hasTarget = TryGetTargetPosition(out var targetPos);
		var vnavAvailable = vnav.IsAvailable;

		if (!FollowDecision.CanRunFollow(enabled, pluginEnabled, playerAvailable, hasTarget, vnavAvailable))
			return;

		if (!FollowDecision.TryFireUpdate(ref nextUpdateMs, now))
			return;

		// Live Config.PartyFollowDistance unless SetFollowDistance / SetFollow(distance>0) override.
		if (!followDistanceOverride)
			followDistance = FollowDecision.ClampFollowDistance(getDefaultFollowDistance());

		var distance = FollowDecision.Distance(player!.Position, targetPos);
		var decision = FollowDecision.DecideFollowTick(
			followEnabled: true,
			pluginEnabled: true,
			playerAvailable: true,
			hasTarget: true,
			vnavAvailable: true,
			throttleReady: true,
			distanceToTarget: distance,
			followDistance: followDistance);

		ApplyDecision(decision, targetPos);
	}

	private void ApplyDecision(FollowTickResult decision, Vector3 targetPos)
	{
		if (decision.StopPath)
			vnav.PathStop();

		if (!decision.MoveToTarget)
			return;

		vnav.PathMoveTo([targetPos], FollowDecision.PreferCanFly);
	}

	private void ApplyDisable()
	{
		var decision = FollowDecision.DecideOnDisable();
		if (decision.StopPath)
			vnav.PathStop();
		nextUpdateMs = 0;
	}

	private void InvalidatePathIfTargetChanged(IGameObject? newTarget)
	{
		if (!FollowDecision.ShouldInvalidatePathOnTargetChange(!ReferenceEquals(followTarget, newTarget)))
			return;

		vnav.PathStop();
		nextUpdateMs = 0;
	}

	private bool TryGetTargetPosition(out Vector3 position)
	{
		position = default;
		var target = followTarget;
		if (target == null)
			return false;

		try
		{
			if (!target.IsValid())
				return false;

			position = target.Position;
			return true;
		}
		catch
		{
			return false;
		}
	}
}
