using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Behavior;

/// <summary>
/// Triggered when a player attempts to enter a settlement
/// </summary>
public record StartSettlementEncounterAttempted : IEvent
{
    public string PartyId { get; }
    public string SettlementId { get; }

    public StartSettlementEncounterAttempted(
        string partyId,
        string settlementId)
    {
        PartyId = partyId;
        SettlementId = settlementId;
    }
}