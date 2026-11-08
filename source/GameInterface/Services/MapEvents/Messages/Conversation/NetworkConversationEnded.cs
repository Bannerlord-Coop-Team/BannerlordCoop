using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Conversation;

/// <summary>
/// Client to Server notification that this client's player encounter finished (or an approved one failed to
/// start). The server releases the AI party held for that request, if any; the sender is identified by its peer.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkConversationEnded : ICommand
{
    [ProtoMember(1)]
    public readonly string RequestId;
    /// <summary>Sent from PlayerEncounter.Finish, not a failed or stale approval.</summary>
    [ProtoMember(2)]
    public readonly bool EncounterFinished;

    public NetworkConversationEnded(string requestId = null, bool encounterFinished = false)
    {
        RequestId = requestId;
        EncounterFinished = encounterFinished;
    }
}
