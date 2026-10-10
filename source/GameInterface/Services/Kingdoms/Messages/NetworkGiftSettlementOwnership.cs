using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Kingdoms.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkGiftSettlementOwnership : ICommand
{
    [ProtoMember(1)]
    public readonly string SettlementId;

    [ProtoMember(2)]
    public readonly string ReceiverClanId;

    public NetworkGiftSettlementOwnership(string settlementId, string receiverClanId)
    {
        SettlementId = settlementId;
        ReceiverClanId = receiverClanId;
    }
}
