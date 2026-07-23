using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Locations.Messages.Conversation;

/// <summary>
/// Server -> Client notification that a location-conversation request was denied because another player is
/// already talking to that NPC. <see cref="Generation"/> echoes the request's id so the client only drops its
/// pending request (and shows the busy message) when the denial still matches its current request, ignoring a
/// stale denial for one it abandoned.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkLocationConversationDenied : ICommand
{
    [ProtoMember(1)]
    public readonly int Generation;

    public NetworkLocationConversationDenied(int generation)
    {
        Generation = generation;
    }
}
