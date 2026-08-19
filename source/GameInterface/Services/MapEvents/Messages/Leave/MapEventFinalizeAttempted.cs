using Common.Messaging;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents.Messages.Leave;

public readonly struct MapEventFinalizeAttempted : IEvent
{
    public readonly MapEvent MapEvent;

    public MapEventFinalizeAttempted(MapEvent mapEvent)
    {
        MapEvent = mapEvent;
    }
}
