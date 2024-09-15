using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobilePartyAIs.Messages;
internal class MobilePartyAiCreated : IEvent
{
    public MobilePartyAiCreated(MobilePartyAi instance, MobileParty party)
    {
        Instance = instance;
        Party = party;
    }

    public MobilePartyAi Instance { get; }
    public MobileParty Party { get; }
}
