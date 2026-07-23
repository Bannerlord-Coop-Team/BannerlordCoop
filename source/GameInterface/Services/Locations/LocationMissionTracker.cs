using System.Runtime.CompilerServices;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.Locations;

/// <summary>
/// Tracks which missions are coop LOCATION missions — the ones <see cref="Patches.PlayerLocationEntryPatches"/>
/// attached the P2P location behaviors to (tavern/indoor, town centre, castle courtyard, village). Static
/// because Harmony patches (e.g. <see cref="Patches.LocationPvpBlockPatch"/>) cannot resolve DI services;
/// keyed weakly on the <see cref="Mission"/> itself so entries die with their mission and no end-of-mission
/// cleanup hook is needed.
/// </summary>
public static class LocationMissionTracker
{
    private static readonly ConditionalWeakTable<Mission, object> LocationMissions =
        new ConditionalWeakTable<Mission, object>();

    /// <summary>Marks a mission as a coop location mission. False when it was already registered.</summary>
    public static bool TryRegister(Mission mission)
    {
        if (mission == null) return false;
        if (LocationMissions.TryGetValue(mission, out _)) return false;

        LocationMissions.Add(mission, null);
        return true;
    }

    /// <summary>True when the mission is a registered coop location mission.</summary>
    public static bool IsLocationMission(Mission mission)
        => mission != null && LocationMissions.TryGetValue(mission, out _);
}
