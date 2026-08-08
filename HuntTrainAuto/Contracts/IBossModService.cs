#nullable enable
using System;
using HuntTrainAuto.Combat;

namespace HuntTrainAuto.Contracts;

/// <summary>
/// BossMod / BossModReborn soft-fail surface.
/// Ownership with RSR: RSR = GCD rotation; BM AI = hunt dodge / safe-zone movement.
/// Enable path sets ForbidActions so BM does not fight RSR for casts.
/// </summary>
public interface IBossModService : IDisposable
{
	/// <summary>True when a preferred BossMod family plugin is loaded.</summary>
	bool IsAvailable { get; }

	/// <summary>Resolved provider (None when neither loaded).</summary>
	BossModProviderKind ActiveProvider { get; }

	/// <summary>
	/// Enable Automovement / AI for the active provider. Soft-fails silently.
	/// When <paramref name="coexistWithRsr"/> is true, also ForbidActions and clear ForbidMovement.
	/// </summary>
	bool EnableAi(bool coexistWithRsr = true);

	/// <summary>Disable Automovement / AI. Soft-fails silently.</summary>
	bool DisableAi();

	/// <summary>
	/// Soft-fail readback of <c>AIConfig.Enabled</c> when Configuration IPC supports get.
	/// Null when unavailable / unreadable (BMR Beh may not expose Enabled).
	/// </summary>
	bool? TryGetAiEnabled();
}
