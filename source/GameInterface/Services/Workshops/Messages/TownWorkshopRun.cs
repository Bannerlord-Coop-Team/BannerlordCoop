using Common.Messaging;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace GameInterface.Services.Workshops.Messages;

public readonly struct TownWorkshopRun : IEvent
{
    public readonly Town Town;
    public readonly Workshop Workshop;

    public TownWorkshopRun(Town town, Workshop workshop)
    {
        Town = town;
        Workshop = workshop;
    }
}
