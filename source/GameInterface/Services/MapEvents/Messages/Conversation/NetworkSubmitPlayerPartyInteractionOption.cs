using Common.Messaging;
using GameInterface.Services.MapEvents.PlayerPartyInteractions;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Conversation;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkSubmitPlayerPartyInteractionOption : ICommand
{
    [ProtoMember(1)]
    public readonly string SessionId;
    [ProtoMember(2)]
    public readonly PlayerPartyInteractionOption Option;
    [ProtoMember(3)]
    public readonly string PartyId;

    public NetworkSubmitPlayerPartyInteractionOption(string sessionId, PlayerPartyInteractionOption option)
        : this(sessionId, option, null)
    {
    }

    public NetworkSubmitPlayerPartyInteractionOption(string sessionId, PlayerPartyInteractionOption option, string partyId)
    {
        SessionId = sessionId;
        Option = option;
        PartyId = partyId;
    }
}
