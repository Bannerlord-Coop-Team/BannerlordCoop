using Common.Messaging;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents.Messages;

public readonly struct MapEventInvolvedPartiesAdded : IEvent
{
    public readonly MapEvent MapEvent;
    public readonly IEnumerable<MapEventParty> AddedParties;

    public MapEventInvolvedPartiesAdded(MapEvent mapEvent, IEnumerable<MapEventParty> addedParties)
    {
        MapEvent = mapEvent;
        AddedParties = addedParties;
    }
}
