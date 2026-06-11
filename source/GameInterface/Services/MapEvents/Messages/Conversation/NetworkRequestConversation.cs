using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Conversation;

/// <summary>
/// Client -> Server request to run <c>PlayerEncounter.RestartPlayerEncounter</c> for the given parties. The server
/// validates it and, if allowed, replies with <see cref="NetworkAllowConversation"/>. Rejected requests receive no
/// response.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRequestConversation : ICommand
{
    [ProtoMember(1)]
    public readonly string DefenderId;
    [ProtoMember(2)]
    public readonly string AttackerId;
    [ProtoMember(3)]
    public readonly bool ForcePlayerOutFromSettlement;
    [ProtoMember(4)]
    public readonly ConversationRestartSource Source;

    public NetworkRequestConversation(string defenderId, string attackerId, bool forcePlayerOutFromSettlement, ConversationRestartSource source)
    {
        DefenderId = defenderId;
        AttackerId = attackerId;
        ForcePlayerOutFromSettlement = forcePlayerOutFromSettlement;
        Source = source;
    }
}
