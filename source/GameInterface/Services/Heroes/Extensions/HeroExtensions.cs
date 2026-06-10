using Common.Caching;
using Common.Logging;
using GameInterface.Services.Entity;
using GameInterface.Services.ObjectManager;
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
    public static bool IsPlayer(this Hero hero)
    {
        if (hero is null)
        {
            Logger.Error("{parameterName} was null", nameof(hero));
            return false;
        }

        return PlayerRegistry.PlayerObjects.TryGetValue(hero, out var _);
    }

    /// <summary>
    /// Check to see if the Hero is controlled by this client or server.
    /// </summary>
    /// <param name="hero">Hero to check that is controlled</param>
    /// <returns>true if is controlled otherwise false.</returns>
    public static bool IsHeroControlled(this Hero hero)
    {
        if (hero is null)
        {
            Logger.Error("{parameterName} was null", nameof(hero));
            return false;
        }

        if (ControlledEntityRegistry.ControlledEntitiesCache.TryGetValue(hero, out var cachedValue))
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

        if (!objectManager.TryGetId(hero, out var heroId))
        {
            Logger.Error("Unable to resolve id for party with StringId {stringId}", hero.StringId);
            return false;
        }

        var result = entityRegistry.IsControlledBy(idProvider.ControllerId, heroId);

        ControlledEntityRegistry.ControlledEntitiesCache.Add(hero, new CachedPrimitive<bool>(result));

        return result;
    }
}
