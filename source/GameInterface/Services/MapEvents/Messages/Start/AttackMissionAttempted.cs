using Common.Messaging;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents.Messages.Start;

internal readonly struct AttackMissionAttempted : IEvent
{
    public readonly MapEvent MapEvent;

    public AttackMissionAttempted(MapEvent mapEvent)
    {
        MapEvent = mapEvent;
    }
}
