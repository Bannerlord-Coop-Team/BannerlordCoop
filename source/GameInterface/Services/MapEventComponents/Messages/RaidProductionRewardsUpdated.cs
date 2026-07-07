using Common.Messaging;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEventComponents.Messages;

internal readonly struct RaidProductionRewardsUpdated : IEvent
{
    public readonly RaidEventComponent Component;

    public RaidProductionRewardsUpdated(RaidEventComponent component)
    {
        Component = component;
    }
}