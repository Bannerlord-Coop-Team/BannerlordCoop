using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.BesiegerCamps.Messages;
internal class BesiegerCampSiegeEventChanged : IEvent
{
    public BesiegerCampSiegeEventChanged(BesiegerCamp besiegerCamp, SiegeEvent siegeEvent)
    {
        BesiegerCamp = besiegerCamp;
        SiegeEvent = siegeEvent;
    }

    public BesiegerCamp BesiegerCamp { get; }
    public SiegeEvent SiegeEvent { get; }
}
