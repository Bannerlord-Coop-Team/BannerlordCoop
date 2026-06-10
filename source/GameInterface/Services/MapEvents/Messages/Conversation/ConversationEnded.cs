using Common.Messaging;

namespace GameInterface.Services.MapEvents.Messages.Conversation;

/// <summary>
/// Local (client-side) notification, published when this client's <c>PlayerEncounter</c> finishes, telling the
/// server to release the AI party held for this player's conversation. Bridged to the network as
/// <see cref="NetworkConversationEnded"/> by <see cref="Handlers.ConversationRequestHandler"/>.
/// </summary>
internal readonly struct ConversationEnded : IEvent
{
}
