using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Behavior;

/// <summary>
/// Triggered when the local player's party attempts to start a party encounter (e.g., talking to a lord on the campaign map)
/// </summary>
public record StartPartyEncounterAttempted : IEvent
{
    public string AttackerPartyId { get; }
    public string DefenderPartyId { get; }

    public StartPartyEncounterAttempted(string attackerPartyId, string defenderPartyId)
    {
        AttackerPartyId = attackerPartyId;
        DefenderPartyId = defenderPartyId;
    }
}
