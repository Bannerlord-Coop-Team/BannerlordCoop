using NavalDLC.Missions.Objects;
using System.Collections.Generic;

namespace Missions.Naval;

/// <summary>
/// [Game thread] The hulls in the current mission that another owner simulates. Static because the Harmony
/// gates read it; hulls that never registered here (the DEBUG lab's fixtures) stay on the vanilla path.
/// </summary>
internal static class NavalForeignHulls
{
    private static readonly HashSet<MissionShip> hulls = new HashSet<MissionShip>();

    public static IEnumerable<MissionShip> All => hulls;

    public static void Add(MissionShip ship) => hulls.Add(ship);

    public static bool Contains(MissionShip ship) => ship != null && hulls.Contains(ship);

    public static void Clear() => hulls.Clear();
}
