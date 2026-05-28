using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEventSides.Messages;

public readonly struct MapEventPartyBattlePartyAdded : IEvent
{
    public readonly MapEventSide MapEventSide;
    public readonly MapEventParty MapEventParty;

    public MapEventPartyBattlePartyAdded(MapEventSide mapEventSide, MapEventParty mapEventParty)
    {
        MapEventSide = mapEventSide;
        MapEventParty = mapEventParty;
    }
}
