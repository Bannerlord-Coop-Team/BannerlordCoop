using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Conversation;

/// <summary>
/// Server to Client notification that a conversation request was denied. The client shows the player why their
/// interaction did nothing; the request id scopes cleanup so a delayed denial cannot clear a newer retry.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkConversationDenied : ICommand
{
    [ProtoMember(1)]
    public readonly ConversationDeniedReason Reason;
    [ProtoMember(2)]
    public readonly string RequestId;

    public NetworkConversationDenied(ConversationDeniedReason reason, string requestId)
    {
        Reason = reason;
        RequestId = requestId;
    }
}

internal enum ConversationDeniedReason
{
    PartyEngaged,
    PlayerUnavailable
}
