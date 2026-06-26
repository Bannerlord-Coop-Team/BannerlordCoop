using Common.Messaging;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents.Messages;

/// <summary>
/// [Client local] A side surrendered in a map event the conversing client is in (e.g. accepting a
/// bandit surrender). The capture that follows is server-authoritative, so this is forwarded so the
/// server can mark the side as surrendered before it captures — otherwise the server captures at the
/// reduced non-surrender rate and only a fraction of the troops become prisoners.
/// </summary>
public readonly struct MapEventSurrenderAttempted : IEvent
{
    public readonly MapEvent MapEvent;
    public readonly BattleSideEnum Side;

    public MapEventSurrenderAttempted(MapEvent mapEvent, BattleSideEnum side)
    {
        MapEvent = mapEvent;
        Side = side;
    }
}
