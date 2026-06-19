using Common.Messaging;

namespace GameInterface.Services.Locations.Messages.Conversation;

/// <summary>
/// Local (client-side) notification, published when this client's held location conversation finishes,
/// telling the server to release the NPC. Bridged to the network as
/// <see cref="NetworkLocationConversationEnded"/> by <see cref="Handlers.LocationConversationHandler"/>.
/// </summary>
internal readonly struct LocationConversationEnded : IEvent
{
}
