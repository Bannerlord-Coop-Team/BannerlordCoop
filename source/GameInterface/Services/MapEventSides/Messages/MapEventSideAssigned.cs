using Common.Messaging;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEventSides.Messages;

public readonly struct MapEventSideAssigned : IEvent
{
    public readonly MapEvent MapEvent;
    public readonly MapEventSide MapEventSide;
    public readonly BattleSideEnum Side;

    public MapEventSideAssigned(MapEvent mapEvent, MapEventSide mapEventSide, BattleSideEnum side)
    {
        MapEvent = mapEvent;
        MapEventSide = mapEventSide;
        Side = side;
    }
}
