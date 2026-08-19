using Common.Logging;
using GameInterface.Services.Clans.Extensions;
using Serilog;
using System.Linq;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Kingdoms.Extentions;

internal static class KingdomExtensions
{
    private static readonly ILogger Logger = LogManager.GetLogger<Kingdom>();

    /// <summary>
    /// Checks to see if the Faction includes a player controlled clan, either in a kingdom or if just a clan
    /// </summary>
    public static bool IsPlayerFaction(this IFaction faction)
    {
        if (faction is Kingdom kingdomFaction)
        {
            foreach (var clan in kingdomFaction._clans)
            {
                if (clan.IsPlayerClan()) return true;
            }
        }
        else if (faction is Clan clanFaction)
        {
            return clanFaction.IsPlayerClan();
        }

        return false;
    }

    /// <summary>
    /// Checks to see if the Kingdom includes a player controlled clan
    /// </summary>
    public static bool IsPlayerKingdom(this Kingdom kingdom)
    {
        if (kingdom is null) return false;

        return kingdom.Clans.Any(clan => clan.IsPlayerClan());
    }
}
