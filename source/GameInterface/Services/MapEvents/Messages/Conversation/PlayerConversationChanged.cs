using Common.Messaging;

namespace GameInterface.Services.MapEvents.Messages.Conversation;

/// <summary>
/// [Server] Raised after a player party enters or leaves an AI or player conversation.
/// </summary>
public readonly struct PlayerConversationChanged : IEvent
{
}
