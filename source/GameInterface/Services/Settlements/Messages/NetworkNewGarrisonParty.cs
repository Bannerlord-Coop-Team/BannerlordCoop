using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Settlements.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkNewGarrisonParty : ICommand
{
    [ProtoMember(1)]
    public readonly string SettlementId;

    public NetworkNewGarrisonParty(string settlementId)
    {
        SettlementId = settlementId;
    }
}
