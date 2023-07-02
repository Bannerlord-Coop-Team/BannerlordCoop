using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;

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
