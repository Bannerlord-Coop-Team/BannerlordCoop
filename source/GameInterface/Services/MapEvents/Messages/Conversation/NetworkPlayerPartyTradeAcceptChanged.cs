using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Conversation;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkPlayerPartyTradeAcceptChanged : ICommand
{
    [ProtoMember(1)]
    public readonly string SessionId;
    [ProtoMember(2)]
    public readonly bool Accepted;

    public NetworkPlayerPartyTradeAcceptChanged(string sessionId, bool accepted)
    {
        SessionId = sessionId;
        Accepted = accepted;
    }
}
