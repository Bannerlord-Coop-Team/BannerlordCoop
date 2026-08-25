using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Armies.Messages;

public readonly struct ArmyFullyCreated : IEvent
{
    public readonly Army Army;

    public ArmyFullyCreated(Army army)
    {
        Army = army;
    }
}
