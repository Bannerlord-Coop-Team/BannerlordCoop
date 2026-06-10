using Common;
using Common.Caching;
using Common.Logging;
using GameInterface.Services.Entity;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Heroes.Extensions;

internal static class HeroExtensions
{
    private static readonly ILogger Logger = LogManager.GetLogger<MobileParty>();

    /// <summary>
    /// Checks to see if the Hero is controlled by a player.
    /// </summary>
    /// <param name="party">The mobile party that may be controlled by a player</param>
    /// <returns>return true if the MobileParty is a player otherwise false.</returns>
    public static bool IsPlayerHero(this Hero hero)
    {
        if (hero is null)
        {
            Logger.Error("{parameterName} was null", nameof(hero));
            return false;
        }

        return PlayerManager.TryGetControlledObjectInfo(hero, out var _);
    }

    /// <summary>
    /// Check to see if the Hero is controlled by this client or server.
    /// </summary>
    /// <param name="hero">Hero to check that is controlled</param>
    /// <returns>true if is controlled otherwise false.</returns>
    public static bool IsControlledByThisInstance(this Hero hero)
    {
        if (hero is null)
        {
            Logger.Error("{parameterName} was null", nameof(hero));
            return false;
        }

        if (ModInformation.IsServer)
        {
            // Server controls all non-player objects
            return !PlayerManager.TryGetControlledObjectInfo(hero, out var _);
        }

        if (!PlayerManager.TryGetControlledObjectInfo(hero, out var controlledObjectInfo))
            return false;

        return controlledObjectInfo.IsControlled;
    }
}
