using Common.Messaging;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEventParties.Messages;

internal readonly struct MapEventTroopsUpdated : IEvent
{
    public readonly MapEventParty MapEventParty;

    public MapEventTroopsUpdated(MapEventParty mapEventParty)
    {
        MapEventParty = mapEventParty;
    }
}
