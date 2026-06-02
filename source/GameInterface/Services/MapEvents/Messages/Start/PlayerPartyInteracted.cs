using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Messages.Start;

internal readonly struct PlayerPartyInteracted : IEvent
{
    public readonly MobileParty RequestingParty;
    public readonly MobileParty TargetParty;

    public PlayerPartyInteracted(MobileParty requestingParty, MobileParty receivingParty)
    {
        RequestingParty = requestingParty;
        TargetParty = receivingParty;
    }
}
