using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MobileParties.Messages.Lifetime;

[ProtoContract]
internal readonly struct NetworkPartyDisbanded : ICommand
{
    [ProtoMember(1)]
    public readonly string DisbandedPartyId;
    [ProtoMember(2)]
    public readonly string RelatedSettlementId;

    public NetworkPartyDisbanded(string disbandedPartyId, string relatedSettlementId)
    {
        DisbandedPartyId = disbandedPartyId;
        RelatedSettlementId = relatedSettlementId;
    }
}
