using Common.Messaging;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEventParties.Messages
{
    internal record MapEventPartyCreated : IEvent
    {
        public MapEventParty MapEventParty { get; }
        public PartyBase PartyBase { get; }

        public MapEventPartyCreated(MapEventParty mapEventParty, PartyBase partyBase)
        {
            MapEventParty = mapEventParty;
            PartyBase = partyBase;
        }
    }
}