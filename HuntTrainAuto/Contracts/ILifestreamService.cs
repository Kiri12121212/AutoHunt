#nullable enable
using System;

namespace HuntTrainAuto.Contracts;

/// <summary>Lifestream plugin IPC surface.</summary>
public interface ILifestreamService : IDisposable
{
	bool IsAvailable { get; }

	bool Teleport(uint aetheryteId, byte subIndex = 0);

	void ChangeInstance(int instance);

	int GetCurrentInstance();

	int GetNumberOfInstances();

	bool CanChangeInstance();

	bool IsBusy();

	/// <summary>
	/// Abort the current Lifestream task / follow path (<c>Lifestream.Abort</c>).
	/// Soft-fails when Lifestream is absent.
	/// </summary>
	void Abort();

	/// <summary>
	/// Whether <paramref name="world"/> is a same-data-center visit target
	/// (<c>Lifestream.CanVisitSameDC</c>).
	/// </summary>
	bool CanVisitSameDC(string world);

	/// <summary>
	/// Whether <paramref name="world"/> is a cross-data-center visit target
	/// (<c>Lifestream.CanVisitCrossDC</c>).
	/// </summary>
	bool CanVisitCrossDC(string world);

	/// <summary>
	/// Queue a world visit (<c>Lifestream.ChangeWorld</c>). Soft-fails (false) when
	/// Lifestream is absent, busy, or the world is not visitable.
	/// </summary>
	bool ChangeWorld(string world);
}
