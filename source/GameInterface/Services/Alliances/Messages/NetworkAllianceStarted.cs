using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Alliances.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkAllianceStarted : ICommand
{
    [ProtoMember(1)]
    public readonly string ProposerKingdomId;
    [ProtoMember(2)]
    public readonly string ReceiverKingdomId;

    public NetworkAllianceStarted(string proposerKingdomId, string receiverKingdomId)
    {
        ProposerKingdomId = proposerKingdomId;
        ReceiverKingdomId = receiverKingdomId;
    }
}
