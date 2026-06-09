using Common;
using Common.Logging;
using GameInterface.Services.Players;
using Serilog;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Extensions;

public static class MobilePartyExtensions
{
    private static readonly ILogger Logger = LogManager.GetLogger<MobileParty>();

    /// <summary>
    /// Check to see if the Party is controlled by this client or server.
    /// </summary>
    /// <param name="party">MobileParty to check that is controlled</param>
    /// <returns>true if is controlled otherwise false.</returns>
    public static bool IsControlledByThisInstance(this MobileParty party)
    {
        if (party is null)
        {
            Logger.Error("{parameterName} was null", nameof(party));
            return false;
        }

        if (ModInformation.IsServer)
        {
            // Server controls all non-player objects
            return !PlayerManager.TryGetControlledObjectInfo(party, out var _);
        }

        if (!PlayerManager.TryGetControlledObjectInfo(party, out var controlledObjectInfo))
            return false;

        return controlledObjectInfo.IsControlled;
    }

    /// <summary>
    /// Checks to see if the MobileParty is controlled by a player.
    /// </summary>
    /// <param name="party">The mobile party that may be controlled by a player</param>
    /// <returns>return true if the MobileParty is a player otherwise false.</returns>
    public static bool IsPlayerParty(this MobileParty party)
    {
        if (party is null)
        {
            Logger.Error("{parameterName} was null", nameof(party));
            return false;
        }

        return PlayerManager.TryGetControlledObjectInfo(party, out var _);
    }
}