using Common.Logging;
using GameInterface.Services.Entity;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Serilog;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Extensions;

internal static class PartyExtensions
{
    private static ILogger Logger = LogManager.GetLogger<MobileParty>();


    /// <summary>
    /// Check to see if the Party is controlled by a specific MobileParty
    /// </summary>
    /// <param name="party">MobileParty to check that is controlled</param>
    /// <returns>true if is controlled otherwise false.</returns>
    public static bool IsPartyControlled(this MobileParty party)
    {
        // Allow method if container or registry cannot be resolved
        if (ContainerProvider.TryResolve<IControlledEntityRegistry>(out var entityRegistry) == false)
        {
            Logger.Error("Unable to resolve {name}", nameof(IControlledEntityRegistry));
            return false;

        }
        if (ContainerProvider.TryResolve<IControllerIdProvider>(out var idProvider) == false)
        {
            Logger.Error("Unable to resolve {name}", nameof(IControllerIdProvider));
            return false;
        };

        return entityRegistry.IsControlledBy(idProvider.ControllerId, party.StringId);
    }

    /// <summary>
    /// Checks to see if the MobileParty is controlled by a player.
    /// </summary>
    /// <param name="party">The mobile party that may be controlled by a player</param>
    /// <returns>return true if the MobileParty is a player otherwise false.</returns>
    public static bool IsPlayerParty(this MobileParty party)
    {
        // Allow method if container or registry cannot be resolved
        if (ContainerProvider.TryResolve<IPlayerRegistry>(out var playerRegistry) == false)
        {
            Logger.Error("Unable to resolve {name}", nameof(IPlayerRegistry));
            return false;
        };

        return playerRegistry.Contains(party);
    }
}
