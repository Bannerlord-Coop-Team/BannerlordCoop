using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Conversation;

/// <summary>
/// Server to Client notification that a conversation request was denied. The client shows the player why their
/// interaction did nothing; the request is identified implicitly (one outstanding request per client at a time).
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkConversationDenied : ICommand
{
    [ProtoMember(1)]
    public readonly ConversationDeniedReason Reason;

    public NetworkConversationDenied(ConversationDeniedReason reason)
    {
        Reason = reason;
    }
}

internal enum ConversationDeniedReason
{
    PartyEngaged,
    PlayerUnavailable
}
