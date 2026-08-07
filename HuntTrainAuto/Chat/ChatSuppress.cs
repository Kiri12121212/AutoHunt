#nullable enable

namespace HuntTrainAuto.Chat;

/// <summary>
/// Pure suppress predicate (HTA parity). No Dalamud types — unit-testable.
/// </summary>
public static class ChatSuppress
{
	/// <summary>
	/// When true, the chat handler should call <c>PreventOriginal()</c> on the message.
	/// Map-link and conductor messages are never suppressed; requires a non-empty conductor list.
	/// </summary>
	public static bool ShouldSuppress(
		bool suppressChatOtherPlayers,
		bool isMapLink,
		bool isConductorMessage,
		int conductorCount)
		=> suppressChatOtherPlayers
			&& !isMapLink
			&& !isConductorMessage
			&& conductorCount > 0;
}
