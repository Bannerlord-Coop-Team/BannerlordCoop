using GameInterface.Services.Clans.Extensions;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Kingdoms.Extentions;

internal static class KingdomExtensions
{
    public static bool IsPlayerKingdom(this Kingdom kingdom)
    {
        if (kingdom is null) return false;

        return kingdom.Clans.Any(clan => clan.IsPlayerClan());
    }
    public static bool IsPlayerKingdomAndNotMercenary(this Kingdom kingdom, out List<Clan> clans)
    {
        clans = null;

        if (kingdom is null) return false;

        clans = kingdom.Clans.Where(clan => clan.IsPlayerClan() && !clan.IsUnderMercenaryService).ToList();

        return clans.Count > 0;
    }
}
