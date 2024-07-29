using Common.Messaging;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents.Messages;
internal record MapEventDestroyed(MapEvent Instance) : IEvent
{
    public MapEvent Instance { get; } = Instance;
}