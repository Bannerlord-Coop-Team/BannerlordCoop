using Common.Messaging;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEventComponents.Messages;

internal readonly struct LiftPlayerBlockadeAttempted : IEvent
{
    public readonly MapEvent MapEvent;

    public LiftPlayerBlockadeAttempted(MapEvent mapEvent)
    {
        MapEvent = mapEvent;
    }
}
