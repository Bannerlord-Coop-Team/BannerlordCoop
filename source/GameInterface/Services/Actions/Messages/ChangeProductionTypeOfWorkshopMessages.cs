using Common.Messaging;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace GameInterface.Services.Actions.Messages;

public readonly struct ProductionTypeOfWorkshopChanged : IEvent
{
    public readonly Workshop Workshop;
    public readonly WorkshopType WorkshopType;
    public readonly bool IgnoreCost;

    public ProductionTypeOfWorkshopChanged(
        Workshop workshop,
        WorkshopType workshopType,
        bool ignoreCost)
    {
        Workshop = workshop;
        WorkshopType = workshopType;
        IgnoreCost = ignoreCost;
    }
}
