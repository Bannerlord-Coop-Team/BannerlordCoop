using Common.Messaging;
using GameInterface.Services.MobileParties.Data;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Messages
{
    internal record PartyAiBehaviorChanged : IEvent
    {
        public MobileParty Party { get; }
        public AiBehaviorUpdateData BehaviorUpdateData { get; }

        public PartyAiBehaviorChanged(MobileParty party, AiBehaviorUpdateData behaviorUpdateData)
        {
            Party = party;
            BehaviorUpdateData = behaviorUpdateData;
        }
    }
}
