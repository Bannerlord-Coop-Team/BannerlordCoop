using Common.Messaging;
using GameInterface.Services.MapEvents.PlayerPartyInteractions;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Conversation;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkPlayerPartyInteractionDenied : ICommand
{
    [ProtoMember(1)]
    public readonly PlayerPartyInteractionDeniedReason Reason;

    public NetworkPlayerPartyInteractionDenied(PlayerPartyInteractionDeniedReason reason)
    {
        Reason = reason;
    }
}
