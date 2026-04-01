using Common.Messaging;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEventSides.Messages
{
    internal record MapEventPartyAdded : IEvent
    {
        public MapEventPartyAdded(MapEventSide mapEventSide, MapEventParty mapEventParty)
        {
            MapEventSide = mapEventSide;
            MapEventParty = mapEventParty;
        }

        public MapEventSide MapEventSide { get; }
        public MapEventParty MapEventParty { get; }
    }
}