using GameInterface.Services.MobileParties.Extensions;
using System.Linq;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents;

public static class MapEventExtensions
{
    public static bool ContainsPlayerParty(this MapEvent mapEvent)
    {
        if (mapEvent is null) return false;

        // Prevent any parties from joining a player battle
        return mapEvent.InvolvedParties.Any(party => party.MobileParty?.IsPlayerParty() == true);
    }
}
