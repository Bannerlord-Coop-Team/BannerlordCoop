using Common.Messaging;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents.Messages.Leave;

/// <summary>
/// Published on the server after a map event has been finalized and its parties
/// have left it. The party whose removal finalized the event can still point at it
/// until vanilla's <c>PartyBase.MapEventSide</c> setter returns.
/// </summary>
public readonly struct MapEventFinalized : IEvent
{
    public readonly MapEvent MapEvent;

    public MapEventFinalized(MapEvent mapEvent)
    {
        MapEvent = mapEvent;
    }
}
