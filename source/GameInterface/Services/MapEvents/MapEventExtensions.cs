using GameInterface.Services.MobileParties.Extensions;
using System.Linq;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents;

public static class MapEventExtensions
{
    public static bool ContainsPlayerParty(this MapEvent mapEvent)
    {
        if (mapEvent is null) return false;

        if (mapEvent.InvolvedParties.Any(party => party?.MobileParty?.IsPlayerParty() == true))
            return true;

        return false;
    }
}
