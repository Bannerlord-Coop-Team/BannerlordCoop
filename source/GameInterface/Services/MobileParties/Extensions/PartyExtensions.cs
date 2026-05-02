using Common.Logging;
using GameInterface.Services.Entity;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Serilog;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Extensions;

internal static class PartyExtensions
{
    private static readonly ILogger Logger = LogManager.GetLogger<MobileParty>();

    private static ConditionalWeakTable<MobileParty, PartyCache> Cache = new();

    private sealed class ControlledCache
    {
        public bool? IsPartyControlled = null;
    }

    private sealed class PlayerCache
    {
        public bool? IsPlayerParty = null;
    }

    /// <summary>
    /// Check to see if the Party is controlled by a specific MobileParty.
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

        //var cache = _controlledCache.GetOrCreateValue(party);

        //if (cache.IsPartyControlled.HasValue)
        //{
        //    return cache.IsPartyControlled.Value;
        //}

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
            Logger.Error("Unable to resolve id for {name}", party.Name);
            return false;
        }

        var result = entityRegistry.IsControlledBy(idProvider.ControllerId, partyId);
        cache.IsPartyControlled = result;

        return result;
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

        //var cache = _playerCache.GetOrCreateValue(party);

        //if (cache.IsPlayerParty.HasValue)
        //{
        //    return cache.IsPlayerParty.Value;
        //}

        if (!ContainerProvider.TryResolve<IPlayerRegistry>(out var playerRegistry))
        {
            Logger.Error("Unable to resolve {name}", nameof(IPlayerRegistry));
            return false;
        }

        var result = playerRegistry.Contains(party);
        //cache.IsPlayerParty = result;

        return result;
    }

    /// <summary>
    /// Clears all cached values for a specific party.
    /// Call this when party ownership/player status may have changed.
    /// </summary>
    public static void InvalidateCache(this MobileParty party)
    {
        if (party is null)
        {
            Logger.Error("{parameterName} was null", nameof(party));
            return;
        }

        _playerCache.Remove(party);
        _controlledCache.Remove(party);
    }

    public static void InvalidateCache()
    {
        Cache = new();
    }
}