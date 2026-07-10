using Common.Messaging;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents.Messages.Leave;

public readonly struct CommitMapEventResults : IEvent
{
    public readonly MapEvent MapEvent;

    public CommitMapEventResults(MapEvent mapEvent)
    {
        MapEvent = mapEvent;
    }
}
