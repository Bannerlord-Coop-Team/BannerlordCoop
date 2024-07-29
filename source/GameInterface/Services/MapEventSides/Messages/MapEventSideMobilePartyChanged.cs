using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEventSides.Messages;
internal class MapEventSideMobilePartyChanged : IEvent
{
    public MapEventSideMobilePartyChanged(MapEventSide mapEventSide, MobileParty mobileParty)
    {
        MapEventSide = mapEventSide;
        MobileParty = mobileParty;
    }

    public MapEventSide MapEventSide { get; }
    public MobileParty MobileParty { get; }
}
