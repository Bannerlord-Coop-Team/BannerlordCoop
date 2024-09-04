using Common.Messaging;

namespace GameInterface.Services.Settlements.Messages;

/// <summary>
/// Change Settlement.LastAttackerParty
/// </summary>
public record ChangeSettlementLastAttackerParty : ICommand
{

    public string SettlementId { get; }
    public string AttackerPartyId { get; }

    public ChangeSettlementLastAttackerParty(string settlementId, string attackerPartyId)
    {
        SettlementId = settlementId;
        AttackerPartyId = attackerPartyId;
    }
}
