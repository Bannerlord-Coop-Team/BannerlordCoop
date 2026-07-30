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
    [ProtoMember(3)]
    public string InteractionId { get; }

    public NetworkRequestBesiegeSettlement(
        string partyId,
        string settlementId,
        string interactionId)
    {
        PartyId = partyId;
        SettlementId = settlementId;
        InteractionId = interactionId;
    }
}
