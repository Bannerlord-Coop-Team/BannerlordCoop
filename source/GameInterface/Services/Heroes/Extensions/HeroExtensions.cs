using Common;
using Common.Logging;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.Players;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Heroes.Extensions;

public static class HeroExtensions
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

    /// <summary>
    /// Checks for local player hero as well heroes in a player's party.
    /// Hero health is owned by the party leader's client in battle.
    /// </summary>
    public static bool IsHealthControlledByThisInstance(this Hero hero)
    {
        if (hero is null)
        {
            Logger.Error("{parameterName} was null", nameof(hero));
            return false;
        }

        // Hero is this player's character, don't need to check party
        if (hero.IsControlledByThisInstance()) return true;

        // Don't manage health for other players in this player's party (for future)
        if (hero.IsPlayerHero()) return false;

        var party = hero.PartyBelongedTo;

        return party != null && party.IsControlledByThisInstance();
    }
}
