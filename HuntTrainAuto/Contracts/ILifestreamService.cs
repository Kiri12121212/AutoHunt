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
}
