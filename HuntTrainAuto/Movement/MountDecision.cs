#nullable enable

namespace HuntTrainAuto.Movement;

/// <summary>Outcome of resolving <see cref="Configuration.Mount"/> against unlocks.</summary>
public enum MountResolveKind
{
	/// <summary>Use GeneralAction 9 / random mount (<c>Mount == 0</c> or multi-unlock fallback).</summary>
	Random,

	/// <summary>Summon a specific unlocked mount id.</summary>
	Specific,

	/// <summary>No unlocked mounts — soft-skip with warning.</summary>
	NoUnlocked,
}

/// <summary>Result of <see cref="MountDecision.ResolveMount"/>.</summary>
public readonly struct MountResolveResult
{
	public required MountResolveKind Kind { get; init; }

	/// <summary>Specific mount RowId when <see cref="Kind"/> is <see cref="MountResolveKind.Specific"/>.</summary>
	public int MountId { get; init; }

	/// <summary>Configured mount was locked (or non-zero) and we fell back.</summary>
	public bool FellBack { get; init; }

	/// <summary>Configured id that was locked when <see cref="FellBack"/>.</summary>
	public int RequestedMountId { get; init; }
}

/// <summary>Framework tick outcome for HTA <c>MountIfCan</c>.</summary>
public enum MountTickKind
{
	/// <summary>Done / skipped — clear the session.</summary>
	Done,

	/// <summary>Wait another tick (throttle / transition / animation lock).</summary>
	Wait,

	/// <summary>Summon via GeneralAction 9 (random).</summary>
	SummonRandom,

	/// <summary>Summon via <c>/mount "Name"</c> for <see cref="MountTickResult.SummonMountId"/>.</summary>
	SummonSpecific,
}

/// <summary>Result of <see cref="MountDecision.DecideMountTick"/>.</summary>
public readonly struct MountTickResult
{
	public required MountTickKind Kind { get; init; }

	/// <summary>When <see cref="Kind"/> is <see cref="MountTickKind.SummonSpecific"/>.</summary>
	public int SummonMountId { get; init; }

	/// <summary>Force-extend CheckMount throttle (HTA transition/casting → 2000ms).</summary>
	public bool ForceCheckThrottle { get; init; }

	/// <summary>Log unlock fallback warning.</summary>
	public bool WarnFallback { get; init; }

	/// <summary>Log no-unlocked-mounts warning.</summary>
	public bool WarnNoMounts { get; init; }

	/// <summary>Configured id for fallback warning text.</summary>
	public int RequestedMountId { get; init; }

	/// <summary>Chosen fallback id for warning text (0 = random).</summary>
	public int FallbackMountId { get; init; }
}

/// <summary>
/// Pure mount decision helpers (HTA <c>TaskMount</c> / <c>MountIfCan</c>).
/// Condition / ActionManager / excel wiring stays in the Framework runner.
/// </summary>
public static class MountDecision
{
	/// <summary>HTA: never mount (<c>Config.Mount == -1</c>).</summary>
	public const int NeverMount = -1;

	/// <summary>HTA: random / GeneralAction 9 (<c>Config.Mount == 0</c>).</summary>
	public const int RandomMount = 0;

	/// <summary>GeneralAction id for Mount.</summary>
	public const uint MountGeneralActionId = 9;

	/// <summary>HTA <c>EzThrottler.Throttle("CheckMount", 2000, true)</c> while transitioning/casting.</summary>
	public const int CheckMountCooldownMs = 2000;

	/// <summary>HTA default <c>EzThrottler.Throttle("SummonMount")</c> interval.</summary>
	public const int SummonCooldownMs = 500;

	/// <summary>Soft session timeout so a stuck mount job cannot run forever.</summary>
	public const int SessionTimeoutMs = 60_000;

	/// <summary>HTA <c>EnqueueIfEnabled</c> gate on <c>Config.UseMount</c>.</summary>
	public static bool ShouldEnqueueIfEnabled(bool useMount) => useMount;

	/// <summary>
	/// Combat-phase falling edge while <paramref name="useMount"/> —
	/// remount ASAP after a kill (ready for next flag / follow), not wait Idle on ground.
	/// Sample <paramref name="wasInCombatPhase"/> before <c>CombatTransitionHelper.Tick</c>;
	/// abort <c>Clear()</c> between frames does not false-trigger (was already false).
	/// </summary>
	public static bool ShouldEnqueueOnCombatEnd(
		bool wasInCombatPhase,
		bool inCombatPhase,
		bool useMount)
		=> useMount && wasInCombatPhase && !inCombatPhase;

	/// <summary>
	/// Flag arrival clears pending mount so AlreadyClose enqueue cannot remount after unmount.
	/// Once <paramref name="readyForGroundFollow"/>, keep combat-end remount while still at the flag.
	/// </summary>
	public static bool ShouldClearMountOnArrival(bool isArrived, bool readyForGroundFollow)
		=> isArrived && !readyForGroundFollow;

	/// <summary>
	/// Remount while still traveling to the flag (Mount/Navigate) if dismounted and no job.
	/// Recovers mount-timeout / post-TP WaitReady abandon so fly-to can start.
	/// Skips engage divert, flag arrival, and unmount (ground approach).
	/// </summary>
	public static bool ShouldEnqueueForFlagTravel(
		bool useMount,
		bool mounted,
		bool mountJobActive,
		bool divertingToEngage,
		bool withinFlagArrival = false,
		bool unmountActive = false,
		bool readyForGroundFollow = false)
		=> useMount
			&& !mounted
			&& !mountJobActive
			&& !divertingToEngage
			&& !withinFlagArrival
			&& !unmountActive
			&& !readyForGroundFollow;

	/// <summary>
	/// Wait-for-player before <c>MountIfCan</c>: screen + player ready, instance-change idle.
	/// <paramref name="lifestreamBusy"/> is ignored — <c>InstanceChangeRunner</c> owns LS exclusivity.
	/// Stale <c>IsBusy</c> after same-zone Teleporter TP was pinning WaitReady and blocking Navigate.
	/// </summary>
	public static bool CanBeginMountAttempt(
		bool lifestreamBusy,
		bool screenReady,
		bool playerReady,
		bool instanceChangeActive)
	{
		_ = lifestreamBusy;
		return screenReady && playerReady && !instanceChangeActive;
	}

	/// <summary>Already mounted or config never-mount → success/no-op.</summary>
	public static bool IsMountCompleteOrSkipped(bool mounted, int mountConfig)
		=> mounted || mountConfig == NeverMount;

	/// <summary>HTA: force CheckMount throttle while in mount transition or casting.</summary>
	public static bool NeedsTransitionWait(bool mountOrOrnamentTransition, bool casting)
		=> mountOrOrnamentTransition || casting;

	/// <summary>GeneralAction 9 usable when status == 0.</summary>
	public static bool IsMountActionUsable(uint actionStatus) => actionStatus == 0;

	/// <summary>
	/// Resolve configured mount against unlocked ids (caller filtered Singular != "").
	/// HTA: locked/0 → single unlock becomes specific; multiple → random; none → soft skip.
	/// </summary>
	/// <param name="configuredMount"><see cref="Configuration.Mount"/> (-1 never, 0 random, else RowId).</param>
	/// <param name="configuredUnlocked">Whether the specific configured id is unlocked (ignored when 0 or -1).</param>
	/// <param name="unlockedCount">Count of unlocked mounts with non-empty Singular.</param>
	/// <param name="firstUnlockedId">First unlocked id when <paramref name="unlockedCount"/> ≥ 1.</param>
	public static MountResolveResult ResolveMount(
		int configuredMount,
		bool configuredUnlocked,
		int unlockedCount,
		uint firstUnlockedId)
	{
		if (configuredMount == NeverMount)
		{
			return new MountResolveResult
			{
				Kind = MountResolveKind.NoUnlocked,
				MountId = NeverMount,
			};
		}

		if (configuredMount != RandomMount && configuredUnlocked)
		{
			return new MountResolveResult
			{
				Kind = MountResolveKind.Specific,
				MountId = configuredMount,
			};
		}

		if (unlockedCount <= 0)
		{
			return new MountResolveResult
			{
				Kind = MountResolveKind.NoUnlocked,
				FellBack = configuredMount != RandomMount,
				RequestedMountId = configuredMount,
			};
		}

		if (unlockedCount == 1)
		{
			var id = (int)firstUnlockedId;
			var fellBack = configuredMount != RandomMount && configuredMount != id;
			return new MountResolveResult
			{
				Kind = MountResolveKind.Specific,
				MountId = id,
				FellBack = fellBack || configuredMount != RandomMount,
				RequestedMountId = configuredMount,
			};
		}

		return new MountResolveResult
		{
			Kind = MountResolveKind.Random,
			MountId = RandomMount,
			FellBack = configuredMount != RandomMount,
			RequestedMountId = configuredMount,
		};
	}

	/// <summary>
	/// One <c>MountIfCan</c> decision step after early complete/skip checks.
	/// Caller applies throttles from <see cref="ForceCheckThrottle"/> / summon fire.
	/// </summary>
	public static MountTickResult DecideMountTick(
		bool mounted,
		int mountConfig,
		bool mountOrOrnamentTransition,
		bool casting,
		bool checkThrottleReady,
		bool mountActionUsable,
		MountResolveResult resolve,
		bool animationLocked,
		bool summonThrottleReady)
	{
		if (IsMountCompleteOrSkipped(mounted, mountConfig))
		{
			return new MountTickResult { Kind = MountTickKind.Done };
		}

		var forceCheck = NeedsTransitionWait(mountOrOrnamentTransition, casting);
		if (forceCheck || !checkThrottleReady)
		{
			return new MountTickResult
			{
				Kind = MountTickKind.Wait,
				ForceCheckThrottle = forceCheck,
			};
		}

		// Transient GA status (post-combat, animation, AM null) — retry, do not abandon.
		// Done-abandon dropped combat-end remount after a single falling-edge enqueue.
		if (!mountActionUsable)
		{
			return new MountTickResult
			{
				Kind = MountTickKind.Wait,
				ForceCheckThrottle = true,
			};
		}

		if (resolve.Kind == MountResolveKind.NoUnlocked)
		{
			return new MountTickResult
			{
				Kind = MountTickKind.Done,
				WarnNoMounts = true,
				WarnFallback = resolve.FellBack,
				RequestedMountId = resolve.RequestedMountId,
			};
		}

		if (animationLocked || !summonThrottleReady)
		{
			return new MountTickResult
			{
				Kind = MountTickKind.Wait,
				WarnFallback = resolve.FellBack,
				RequestedMountId = resolve.RequestedMountId,
				FallbackMountId = resolve.MountId,
			};
		}

		if (resolve.Kind == MountResolveKind.Random)
		{
			return new MountTickResult
			{
				Kind = MountTickKind.SummonRandom,
				WarnFallback = resolve.FellBack,
				RequestedMountId = resolve.RequestedMountId,
				FallbackMountId = RandomMount,
			};
		}

		return new MountTickResult
		{
			Kind = MountTickKind.SummonSpecific,
			SummonMountId = resolve.MountId,
			WarnFallback = resolve.FellBack,
			RequestedMountId = resolve.RequestedMountId,
			FallbackMountId = resolve.MountId,
		};
	}

	/// <summary>Whether CheckMount throttle allows progress (HTA <c>EzThrottler.Check</c>).</summary>
	public static bool IsCheckReady(long nextCheckMs, long nowMs) => nowMs >= nextCheckMs;

	/// <summary>Force CheckMount deadline (extend-only vs existing).</summary>
	public static long ForceCheckThrottle(long nextCheckMs, long nowMs, int cooldownMs = CheckMountCooldownMs)
		=> System.Math.Max(nextCheckMs, nowMs + System.Math.Max(0, cooldownMs));

	/// <summary>Whether SummonMount throttle allows a summon attempt.</summary>
	public static bool TryFireSummon(ref long nextSummonMs, long nowMs, int cooldownMs = SummonCooldownMs)
	{
		if (nowMs < nextSummonMs)
			return false;

		nextSummonMs = nowMs + System.Math.Max(0, cooldownMs);
		return true;
	}

	/// <summary>Session exceeded soft timeout.</summary>
	public static bool IsSessionTimedOut(long deadlineMs, long nowMs)
		=> deadlineMs > 0 && nowMs >= deadlineMs;
}
