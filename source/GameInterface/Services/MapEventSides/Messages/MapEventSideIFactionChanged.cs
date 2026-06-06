using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEventSides.Messages
{
    internal class MapEventSideIFactionChanged : IEvent
    {
        public MapEventSide Side { get; }
        public IFaction Faction { get; }

        public MapEventSideIFactionChanged(MapEventSide side, IFaction faction)
        {
            Side = side;
            Faction = faction;
        }
    }
}