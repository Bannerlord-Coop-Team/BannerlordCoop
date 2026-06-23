using Common.Messaging;
using GameInterface.Services.MapEvents.PlayerPartyInteractions;

namespace GameInterface.Services.MapEvents.Messages.Conversation;

internal readonly struct PlayerPartyInteractionOptionSelected : IEvent
{
    public readonly string SessionId;
    public readonly string PartyId;
    public readonly PlayerPartyInteractionOption Option;

    public PlayerPartyInteractionOptionSelected(string sessionId, PlayerPartyInteractionOption option)
        : this(sessionId, null, option)
    {
    }

    public PlayerPartyInteractionOptionSelected(string sessionId, string partyId, PlayerPartyInteractionOption option)
    {
        SessionId = sessionId;
        PartyId = partyId;
        Option = option;
    }
}
