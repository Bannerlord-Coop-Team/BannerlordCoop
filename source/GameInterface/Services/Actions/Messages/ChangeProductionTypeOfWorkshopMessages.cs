using Common.Messaging;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace GameInterface.Services.Actions.Messages;

public readonly struct ProductionTypeOfWorkshopChanged : IEvent
{
    public readonly Workshop Workshop;
    public readonly WorkshopType WorkshopType;

    public ProductionTypeOfWorkshopChanged(
        Workshop workshop,
        WorkshopType workshopType)
    {
        Workshop = workshop;
        WorkshopType = workshopType;
    }
}
