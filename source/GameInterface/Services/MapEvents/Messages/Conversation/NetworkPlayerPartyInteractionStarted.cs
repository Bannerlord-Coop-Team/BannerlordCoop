using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Conversation;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkPlayerPartyInteractionStarted : ICommand
{
    [ProtoMember(1)]
    public readonly string SessionId;
    [ProtoMember(2)]
    public readonly string InitiatorPartyId;
    [ProtoMember(3)]
    public readonly string ResponderPartyId;
    [ProtoMember(4)]
    public readonly string InitiatorName;
    [ProtoMember(5)]
    public readonly string ResponderName;

    public NetworkPlayerPartyInteractionStarted(
        string sessionId,
        string initiatorPartyId,
        string responderPartyId,
        string initiatorName,
        string responderName)
    {
        SessionId = sessionId;
        InitiatorPartyId = initiatorPartyId;
        ResponderPartyId = responderPartyId;
        InitiatorName = initiatorName;
        ResponderName = responderName;
    }
}
