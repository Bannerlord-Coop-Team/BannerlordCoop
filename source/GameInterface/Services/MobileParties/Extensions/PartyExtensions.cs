using Common;
using Common.Logging;
using GameInterface;
using GameInterface.Services.Entity;
using GameInterface.Services.Players;
using Serilog;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Extensions;

internal static class PartyExtensions
{
    private static readonly ILogger Logger = LogManager.GetLogger<MobileParty>();

    private static readonly ConditionalWeakTable<MobileParty, PartyCache> Cache = new();

    private sealed class PartyCache
    {
        public bool? IsPartyControlled;
        public bool? IsPlayerParty;
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

        var cache = Cache.GetOrCreateValue(party);

        if (cache.IsPartyControlled.HasValue)
        {
            return cache.IsPartyControlled.Value;
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

        var result = entityRegistry.IsControlledBy(idProvider.ControllerId, party.StringId);
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

        var cache = Cache.GetOrCreateValue(party);

        if (cache.IsPlayerParty.HasValue)
        {
            return cache.IsPlayerParty.Value;
        }

        if (!ContainerProvider.TryResolve<IPlayerRegistry>(out var playerRegistry))
        {
            Logger.Error("Unable to resolve {name}", nameof(IPlayerRegistry));
            return false;
        }

        var result = playerRegistry.Contains(party);
        cache.IsPlayerParty = result;

        return result;
    }

    /// <summary>
    /// Clears all cached values for a specific party.
    /// Call this when party ownership/player status may have changed.
    /// </summary>
    public static void InvalidatePartyCache(this MobileParty party)
    {
        if (party is null)
        {
            Logger.Error("{parameterName} was null", nameof(party));
            return;
        }

        Cache.Remove(party);
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

        var cache = Cache.GetOrCreateValue(party);
        cache.IsPartyControlled = null;
    }

    /// <summary>
    /// Clears only the cached player-state for a specific party.
    /// </summary>
    public static void InvalidatePlayerPartyCache(this MobileParty party)
    {
        if (party is null)
        {
            Logger.Error("{parameterName} was null", nameof(party));
            return;
        }

        var cache = Cache.GetOrCreateValue(party);
        cache.IsPlayerParty = null;
    }
}