using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Behavior;

/// <summary>
/// Commands the GameInterface to start a player party encounter (e.g., from server approval to talk to a lord)
/// </summary>
public record StartPartyEncounterCommand : ICommand
{
    public string AttackerPartyId { get; }
    public string DefenderPartyId { get; }

    public StartPartyEncounterCommand(string attackerPartyId, string defenderPartyId)
    {
        AttackerPartyId = attackerPartyId;
        DefenderPartyId = defenderPartyId;
    }
}
