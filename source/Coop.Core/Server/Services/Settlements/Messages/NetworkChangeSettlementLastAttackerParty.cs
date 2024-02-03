using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Settlements.Messages;

/// <summary>
/// Notify client of Settlement.LastAttackerParty change
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkChangeSettlementLastAttackerParty : IEvent
{
    [ProtoMember(1)]
    public string SettlementId { get; }
    [ProtoMember(2)]
    public string AttackerPartyId { get; }

    public NetworkChangeSettlementLastAttackerParty(string settlementId, string attackerPartyId)
    {
        SettlementId = settlementId;
        AttackerPartyId = attackerPartyId;
    }
}