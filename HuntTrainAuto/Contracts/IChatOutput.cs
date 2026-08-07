#nullable enable

namespace HuntTrainAuto.Contracts;

/// <summary>Game chat / slash-command execution.</summary>
public interface IChatOutput
{
	/// <summary>
	/// Execute a slash command. Returns false when empty, not a command, or the call fails.
	/// </summary>
	bool TryExecuteCommand(string command);
}
