using Common.Messaging;
using GameInterface.Services.MobileParties.Data;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace GameInterface.Services.MobileParties.Messages
{
    public record MobilePartyCreated : IEvent
    {
        public MobileParty Party { get; }

        public MobilePartyCreated(MobileParty party)
        {
            Party = party;
        }
    }

    public record MobilePartyDestroyed : IEvent
    {
        public MobileParty Party { get; }
        public PartyBase PartyBase { get; }

        public MobilePartyDestroyed(MobileParty party, PartyBase partyBase)
        {
            Party = party;
            PartyBase = partyBase;
        }
    }
}
