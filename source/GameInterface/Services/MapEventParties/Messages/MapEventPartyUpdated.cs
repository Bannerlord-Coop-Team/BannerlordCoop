using Common.Messaging;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.MapEventParties.Messages;

internal readonly struct MapEventPartyUpdated : IEvent
{
    public readonly MapEventParty MapEventParty;
    public readonly FlattenedTroopRoster Roster;

    public MapEventPartyUpdated(MapEventParty mapEventParty, FlattenedTroopRoster roster)
    {
        MapEventParty = mapEventParty;
        Roster = roster;
    }
}
