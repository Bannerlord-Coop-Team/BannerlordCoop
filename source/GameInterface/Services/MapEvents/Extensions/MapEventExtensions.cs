using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Extensions;

public static class MapEventExtensions
{
    /// <summary>
    /// Finds a party's MapEventParty on either side of the event, without going through PartiesOnSide(side) —
    /// safe to call even after the party's own MapEventSide has already been nulled elsewhere.
    /// </summary>
    public static MapEventParty FindMapEventParty(this MapEvent mapEvent, PartyBase party)
    {
        foreach (var side in mapEvent._sides)
        {
            if (side == null) continue;

            foreach (var mapEventParty in side.Parties)
            {
                if (mapEventParty?.Party == party)
                    return mapEventParty;
            }
        }

        return null;
    }
}
