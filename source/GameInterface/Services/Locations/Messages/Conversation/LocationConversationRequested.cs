using Common.Messaging;

namespace GameInterface.Services.Locations.Messages.Conversation;

/// <summary>
/// Local (client-side) request, published by the location-conversation acquire patch when the player tries
/// to talk to a lock-eligible NPC, asking the server whether the conversation may start. Bridged to the
/// network by <see cref="Handlers.LocationConversationHandler"/>. <see cref="Generation"/> identifies this
/// request so a stale server reply for an abandoned one can be ignored.
/// </summary>
internal readonly struct LocationConversationRequested : IEvent
{
    public readonly string LocationId;
    public readonly string CharacterId;
    public readonly int Generation;

    public LocationConversationRequested(string locationId, string characterId, int generation)
    {
        LocationId = locationId;
        CharacterId = characterId;
        Generation = generation;
    }
}
