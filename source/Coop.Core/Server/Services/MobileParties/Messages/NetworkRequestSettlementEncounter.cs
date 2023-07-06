using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.MobileParties.Messages;

[ProtoContract(SkipConstructor = true)]
internal record NetworkRequestSettlementEncounter : ICommand
{
    [ProtoMember(1)]
    public string PartyId { get; }
    [ProtoMember(2)]
    public string SettlementId { get; }

    public NetworkRequestSettlementEncounter(
        string partyId,
        string settlementId)
    {
        PartyId = partyId;
        SettlementId = settlementId;
    }
}
