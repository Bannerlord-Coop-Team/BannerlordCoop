using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace GameInterface.Services.Actions.Messages;

public readonly struct WorkshopOwnerChanged : IEvent
{
    public readonly Workshop Workshop;
    public readonly Hero NewOwner;
    public readonly WorkshopType WorkshopType;
    public readonly int Capital;
    public readonly int Cost;

    public WorkshopOwnerChanged(Workshop workshop, Hero newOwner, WorkshopType workshopType, int capital, int cost)
    {
        Workshop = workshop;
        NewOwner = newOwner;
        WorkshopType = workshopType;
        Capital = capital;
        Cost = cost;
    }
}
