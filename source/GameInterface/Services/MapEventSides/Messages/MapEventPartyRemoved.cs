using Common.Messaging;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEventSides.Messages
{
    internal record MapEventPartyRemoved : IEvent
    {
        public MapEventPartyRemoved(MapEventSide mapEventSide, MapEventParty mapEventParty)
        {
            MapEventSide = mapEventSide;
            MapEventParty = mapEventParty;
        }

        public MapEventSide MapEventSide { get; }
        public MapEventParty MapEventParty { get; }
    }
}