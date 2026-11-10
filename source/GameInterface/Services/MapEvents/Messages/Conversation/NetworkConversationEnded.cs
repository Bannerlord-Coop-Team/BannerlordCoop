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
    /// <summary>
    /// The map event whose encounter PlayerEncounter.Finish just closed. Null for a failed or stale approval or an
    /// encounter without a battle.
    /// </summary>
    [ProtoMember(2)]
    public readonly string FinishedMapEventId;

    public NetworkConversationEnded(string requestId = null, string finishedMapEventId = null)
    {
        RequestId = requestId;
        FinishedMapEventId = finishedMapEventId;
    }
}
