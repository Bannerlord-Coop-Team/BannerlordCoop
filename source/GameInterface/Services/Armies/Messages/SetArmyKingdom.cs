using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Armies.Messages;

/// <summary>
/// Event for when a Army.AiBehaviorObject changed
/// </summary>
public readonly struct SetArmyKingdom : IEvent
{
    public readonly Army Army;
    public readonly Kingdom Kingdom;

    public SetArmyKingdom(Army army, Kingdom kingdom)
    {
        Army = army;
        Kingdom = kingdom;
    }
}
