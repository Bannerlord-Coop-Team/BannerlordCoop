using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Behavior;

/// <summary>
/// Triggered when a player attempts to leave a settlement
/// </summary>
public record EndSettlementEncounterAttempted : IEvent
{
    public string PartyId { get; }

    public EndSettlementEncounterAttempted(string partyId)
    {
        PartyId = partyId;
    }
}
