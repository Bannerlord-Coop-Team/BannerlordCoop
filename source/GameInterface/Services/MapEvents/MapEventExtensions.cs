using GameInterface.Services.MobileParties.Extensions;
using System.Linq;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents;

public static class MapEventExtensions
{
    public static bool ContainsPlayerParty(this MapEvent mapEvent)
    {
        if (mapEvent is null) return false;

        foreach (var side in mapEvent._sides)
        {
            if (side is null) continue;

            foreach (var eventParty in side.Parties)
            {
                if (eventParty?.Party?.MobileParty?.IsPlayer() == true)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
