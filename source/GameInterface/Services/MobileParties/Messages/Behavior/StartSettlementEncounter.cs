using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Behavior;

/// <summary>
/// Starts a player encounter with a settlement with the current player
/// </summary>
public record StartSettlementEncounter : ICommand
{
    public string PartyId { get; }
    public string SettlementId { get; }

    public StartSettlementEncounter(
        string attackerPartyId,
        string settlementId)
    {
        PartyId = attackerPartyId;
        SettlementId = settlementId;
    }
}