using NavalDLC.Missions.Objects;
using System.Collections.Generic;

namespace Missions.Naval;

/// <summary>
/// [Game thread writes] The hulls in the current mission that another owner simulates. Static because the Harmony
/// gates read it; hulls that never registered here (the DEBUG lab's fixtures) stay on the vanilla path. Copy on write,
/// because the ship damage gate also reads it from hull contact callbacks on physics threads.
/// </summary>
internal static class NavalForeignHulls
{
    private static volatile HashSet<MissionShip> hulls = new HashSet<MissionShip>();

    public static IEnumerable<MissionShip> All => hulls;

    public static void Add(MissionShip ship) => hulls = new HashSet<MissionShip>(hulls) { ship };

    public static void Remove(MissionShip ship)
    {
        var remaining = new HashSet<MissionShip>(hulls);
        remaining.Remove(ship);
        hulls = remaining;
    }

    public static bool Contains(MissionShip ship) => ship != null && hulls.Contains(ship);

    public static void Clear() => hulls = new HashSet<MissionShip>();
}
