using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Conversation;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkPlayerPartyInteractionShown : ICommand
{
    [ProtoMember(1)]
    public readonly string SessionId;
    [ProtoMember(2)]
    public readonly string PartyId;

    public NetworkPlayerPartyInteractionShown(string sessionId, string partyId)
    {
        SessionId = sessionId;
        PartyId = partyId;
    }
}
