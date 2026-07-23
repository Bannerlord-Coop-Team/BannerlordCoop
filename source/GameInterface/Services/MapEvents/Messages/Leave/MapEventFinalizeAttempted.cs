using Common.Messaging;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents.Messages.Leave;

internal readonly struct MapEventFinalizeAttempted : IEvent
{
    public readonly MapEvent MapEvent;

    public MapEventFinalizeAttempted(MapEvent mapEvent)
    {
        MapEvent = mapEvent;
    }
}
