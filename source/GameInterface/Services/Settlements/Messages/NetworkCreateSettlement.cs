using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Settlements.Messages;

[ProtoContract(SkipConstructor = true)]
internal class NetworkCreateSettlement : ICommand
{
    [ProtoMember(1)]
    public string SettlementId { get; }

    public NetworkCreateSettlement(string settlementId)
    {
        SettlementId = settlementId;
    }
}
