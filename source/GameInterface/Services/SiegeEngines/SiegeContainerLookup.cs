using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace GameInterface.Services.SiegeEngines;

/// <summary>One side's engine container paired with the side tag used in its derived registry ids.</summary>
internal readonly struct SiegeEngineSide
{
    public readonly string Side;
    public readonly SiegeEnginesContainer Container;

    public SiegeEngineSide(string side, SiegeEnginesContainer container)
    {
        Side = side;
        Container = container;
    }
}

/// <summary>
/// Shared enumeration over the campaign's active sieges. Every siege service walked
/// <c>Campaign.Current?.SiegeEventManager?.SiegeEvents</c> by hand and repeated the
/// attacker=<see cref="BesiegerCamp"/> / defender=<see cref="Settlement"/> engine-container split;
/// this is the one home for both. Also resolves which besieged settlement a container (or one of its
/// engines) belongs to, since the container has no back-reference and the map visual only refreshes
/// when the owning party is dirtied.
/// </summary>
internal static class SiegeContainerLookup
{
    /// <summary>The active sieges with a resolved besieged settlement, null-guarded.</summary>
    public static IEnumerable<SiegeEvent> ActiveSieges()
    {
        var siegeEvents = Campaign.Current?.SiegeEventManager?.SiegeEvents;
        if (siegeEvents == null) yield break;

        foreach (var siegeEvent in siegeEvents)
        {
            if (siegeEvent?.BesiegedSettlement != null) yield return siegeEvent;
        }
    }

    /// <summary>Both engine containers of a siege with the side tag used in derived ids
    /// (attacker = besieger camp, defender = besieged settlement).</summary>
    public static IEnumerable<SiegeEngineSide> EngineContainers(SiegeEvent siegeEvent)
    {
        var attacker = siegeEvent.BesiegerCamp?.SiegeEngines;
        if (attacker != null) yield return new SiegeEngineSide("attacker", attacker);

        var defender = siegeEvent.BesiegedSettlement?.SiegeEngines;
        if (defender != null) yield return new SiegeEngineSide("defender", defender);
    }

    public static Settlement FindOwnerSettlement(SiegeEnginesContainer container)
    {
        foreach (var siegeEvent in ActiveSieges())
        {
            foreach (var engineSide in EngineContainers(siegeEvent))
            {
                if (engineSide.Container == container) return siegeEvent.BesiegedSettlement;
            }
        }

        return null;
    }

    public static Settlement FindOwnerSettlement(SiegeEngineConstructionProgress siegeEngine)
    {
        foreach (var siegeEvent in ActiveSieges())
        {
            foreach (var engineSide in EngineContainers(siegeEvent))
            {
                if (Owns(engineSide.Container, siegeEngine)) return siegeEvent.BesiegedSettlement;
            }
        }

        return null;
    }

    private static bool Owns(SiegeEnginesContainer container, SiegeEngineConstructionProgress siegeEngine)
    {
        foreach (var candidate in container.AllSiegeEngines())
        {
            if (candidate == siegeEngine) return true;
        }

        return false;
    }
}
