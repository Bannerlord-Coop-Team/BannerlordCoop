using Common.Messaging;

namespace GameInterface.Services.Settlements.Messages;

/// <summary>
///  Changed of Settlement.LastAttackerParty
/// </summary>
public record SettlementChangedLastAttackerParty : ICommand
{
    public string SettlementId { get; }

    public string AttackerPartyId { get; }

    public SettlementChangedLastAttackerParty(string settlementId, string attackerPartyId)
    {
        SettlementId = settlementId;
        AttackerPartyId = attackerPartyId;
    }
}
