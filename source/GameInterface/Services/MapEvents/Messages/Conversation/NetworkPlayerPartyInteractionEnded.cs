using Common.Messaging;
using GameInterface.Services.MapEvents.PlayerPartyInteractions;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Conversation;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkPlayerPartyInteractionEnded : ICommand
{
    [ProtoMember(1)]
    public readonly string SessionId;
    [ProtoMember(2)]
    public readonly string InitiatorPartyId;
    [ProtoMember(3)]
    public readonly string ResponderPartyId;
    [ProtoMember(4)]
    public readonly PlayerPartyInteractionOutcomeType OutcomeType;

    public NetworkPlayerPartyInteractionEnded(
        string sessionId,
        string initiatorPartyId,
        string responderPartyId,
        PlayerPartyInteractionOutcomeType outcomeType)
    {
        SessionId = sessionId;
        InitiatorPartyId = initiatorPartyId;
        ResponderPartyId = responderPartyId;
        OutcomeType = outcomeType;
    }
}
