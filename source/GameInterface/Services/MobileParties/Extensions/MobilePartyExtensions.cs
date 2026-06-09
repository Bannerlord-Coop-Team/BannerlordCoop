using Common.Caching;
using Common.Logging;
using GameInterface.Services.Entity;
using GameInterface.Services.ObjectManager;
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
    public static bool IsPartyControlled(this MobileParty party)
    {
        if (party is null)
        {
            Logger.Error("{parameterName} was null", nameof(party));
            return false;
        }

        if (ControlledEntityRegistry.ControlledEntitiesCache.TryGetValue(party, out var cachedValue))
        {
            return cachedValue.Value;
        }

        if (!ContainerProvider.TryResolve<IControlledEntityRegistry>(out var entityRegistry))
        {
            Logger.Error("Unable to resolve {name}", nameof(IControlledEntityRegistry));
            return false;
        }

        if (!ContainerProvider.TryResolve<IControllerIdProvider>(out var idProvider))
        {
            Logger.Error("Unable to resolve {name}", nameof(IControllerIdProvider));
            return false;
        }

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
        {
            Logger.Error("Unable to resolve {name}", nameof(IObjectManager));
            return false;
        }

        if (!objectManager.TryGetId(party, out var partyId))
        {
            Logger.Error("Unable to resolve id for party with StringId {stringId}", party.StringId);
            return false;
        }

        var result = entityRegistry.IsControlledBy(idProvider.ControllerId, partyId);

        ControlledEntityRegistry.ControlledEntitiesCache.Add(party, new CachedPrimitive<bool>(result));

        return result;
    }

    /// <summary>
    /// Checks to see if the MobileParty is controlled by a player.
    /// </summary>
    /// <param name="party">The mobile party that may be controlled by a player</param>
    /// <returns>return true if the MobileParty is a player otherwise false.</returns>
    public static bool IsPlayer(this MobileParty party)
    {
        if (party is null)
        {
            Logger.Error("{parameterName} was null", nameof(party));
            return false;
        }

        return PlayerRegistry.PlayerObjects.TryGetValue(party, out var _);
    }

    /// <summary>
    /// Clears only the cached controlled-state for a specific party.
    /// </summary>
    public static void InvalidateControlledCache(this MobileParty party)
    {
        if (party is null)
        {
            Logger.Error("{parameterName} was null", nameof(party));
            return;
        }

        ControlledEntityRegistry.ControlledEntitiesCache.Remove(party);
    }
}