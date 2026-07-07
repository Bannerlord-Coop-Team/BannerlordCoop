using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.SiegeEvents.Messages;

/// <summary>
/// Client asks the server to start a siege of a settlement led by its party.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkRequestBesiegeSettlement : ICommand
{
    [ProtoMember(1)]
    public string PartyId { get; }
    [ProtoMember(2)]
    public string SettlementId { get; }

    public NetworkRequestBesiegeSettlement(string partyId, string settlementId)
    {
        PartyId = partyId;
        SettlementId = settlementId;
    }
}
