using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobilePartyAIs.Messages;
internal class MobilePartyAiDestroyed : IEvent
{
    public MobilePartyAiDestroyed(MobilePartyAi instance)
    {
        Instance = instance;
    }

    public MobilePartyAi Instance { get; }
}
