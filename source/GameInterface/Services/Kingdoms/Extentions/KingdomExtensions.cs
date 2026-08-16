using GameInterface.Services.Clans.Extensions;
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
}
