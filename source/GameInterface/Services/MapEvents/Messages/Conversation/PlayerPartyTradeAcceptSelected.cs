using Common.Messaging;

namespace GameInterface.Services.MapEvents.Messages.Conversation;

internal readonly struct PlayerPartyTradeAcceptSelected : IEvent
{
    public readonly string SessionId;
    public readonly bool Accepted;

    public PlayerPartyTradeAcceptSelected(string sessionId, bool accepted)
    {
        SessionId = sessionId;
        Accepted = accepted;
    }
}
