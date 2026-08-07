#nullable enable
using System;

namespace HuntTrainAuto.Contracts;

/// <summary>Teleporter plugin IPC surface.</summary>
public interface ITeleporterService : IDisposable
{
	bool IsAvailable { get; }

	bool Teleport(uint aetheryteId, byte subIndex = 0);
}
