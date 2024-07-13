using Common.Messaging;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents.Messages;
internal record MapEventSideCreated(MapEventSide Instance) : IEvent
{
    public MapEventSide Instance { get; } = Instance;
}