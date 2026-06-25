using GameInterface.Services.MobileParties.Extensions;
using System.Linq;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents;

public static class MapEventExtensions
{
    public static bool ContainsPlayerParty(this MapEvent mapEvent)
    {
        if (mapEvent is null) return false;

        // Prevent any parties from joining a player battle. InvolvedParties yields MapEventParty.Party,
        // which can be null on a receiving co-op machine before it has synced, so guard it.
        return mapEvent.InvolvedParties.Any(party => party?.MobileParty?.IsPlayerParty() == true);
    }
}
