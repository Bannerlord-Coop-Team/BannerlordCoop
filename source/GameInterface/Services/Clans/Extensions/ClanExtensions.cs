using Common;
using Common.Logging;
using GameInterface.Services.Players;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Clans.Extensions;

internal static class ClanExtensions
{
    private static readonly ILogger Logger = LogManager.GetLogger<MobileParty>();

    /// <summary>
    /// Checks to see if the Clan is controlled by a player.
    /// </summary>
    /// <param name="clan">The clan that may be controlled by a player.</param>
    /// <returns>return true if the Clan is player controlled otherwise false.</returns>
    public static bool IsPlayerClan(this Clan clan)
    {
        if (clan is null)
        {
            Logger.Error("{parameterName} was null", nameof(clan));
            return false;
        }

        return PlayerManager.TryGetControlledObjectInfo(clan, out var _);
    }

    /// <summary>
    /// Check to see if the Clan is controlled by this client or server.
    /// </summary>
    /// <param name="clan">Clan to check that is controlled.</param>
    /// <returns>true if is controlled otherwise false.</returns>
    public static bool IsControlledByThisInstance(this Clan clan)
    {
        if (clan is null)
        {
            Logger.Error("{parameterName} was null", nameof(clan));
            return false;
        }

        if (ModInformation.IsServer)
        {
            // Server controls all non-player objects
            return !PlayerManager.TryGetControlledObjectInfo(clan, out var _);
        }

        if (!PlayerManager.TryGetControlledObjectInfo(clan, out var controlledObjectInfo))
            return false;

        return controlledObjectInfo.IsControlled;
    }
}
