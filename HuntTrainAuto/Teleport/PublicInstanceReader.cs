#nullable enable
using System;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using Dalamud.Plugin.Services;
using HuntTrainAuto.Logging;

namespace HuntTrainAuto.Teleport;

/// <summary>
/// Live public-instance readout (same source as Lifestream / Dalamud Setup).
/// <see cref="Dalamud.Plugin.Services.IClientState.Instance"/> only updates on
/// SetCurrentInstance hooks and can stay 0 across whole territories.
/// </summary>
public static class PublicInstanceReader
{
	/// <summary>
	/// Reads <c>UIState.PublicInstance.InstanceId</c>, then
	/// <c>NetworkModuleProxy.GetCurrentInstance</c>. Returns 0 when unsplit / unavailable.
	/// </summary>
	public static unsafe int TryReadInstanceId(
		IPluginLog? pluginLog = null,
		bool enableDebugLogging = false)
	{
		try
		{
			var ui = UIState.Instance();
			if (ui != null)
			{
				var id = (int)ui->PublicInstance.InstanceId;
				if (id > 0)
					return id;
			}

			var framework = Framework.Instance();
			var net = framework != null ? framework->GetNetworkModuleProxy() : null;
			if (net != null)
			{
				var id = net->GetCurrentInstance();
				if (id > 0)
					return id;
			}
		}
		catch (Exception ex)
		{
			// Soft-fail — caller falls back to IPC / ClientState.
			DebugBehavior.Debug(
				pluginLog!,
				enableDebugLogging,
				"Instance",
				$"public instance read failed: {ex.GetType().Name}");
		}

		return 0;
	}
}
