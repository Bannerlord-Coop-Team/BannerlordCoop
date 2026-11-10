using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.AnchorPoints.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkCallFleet : ICommand
{
    [ProtoMember(1)]
    public readonly string MobilePartyId;

    [ProtoMember(2)]
    public readonly string SettlementId;

    public NetworkCallFleet(
        string mobilePartyId,
        string settlementId)
    {
        MobilePartyId = mobilePartyId;
        SettlementId = settlementId;
    }
}
