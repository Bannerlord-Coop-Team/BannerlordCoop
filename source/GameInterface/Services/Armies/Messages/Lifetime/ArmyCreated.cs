using Common.Messaging;
using GameInterface.Services.Armies.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Armies.Messages.Lifetime;

/// <summary>
/// Command to create a new army.
/// </summary>
public record ArmyCreated : ICommand
{
    public ArmyCreated(Army army, Kingdom kingdom, MobileParty party, Army.ArmyTypes armyTypes)
    {
        Army = army;
        Kingdom = kingdom;
        MobileParty = party;
        ArmyType = armyTypes;
    }

    public Army Army { get; }

    public Kingdom Kingdom { get; }
    public MobileParty MobileParty { get; }

    public Army.ArmyTypes ArmyType { get; }
}
