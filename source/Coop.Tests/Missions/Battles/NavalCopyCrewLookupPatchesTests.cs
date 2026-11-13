#if COOP_NAVAL
using HarmonyLib;
using Missions.Naval;
using NavalDLC.Missions.Objects;
using System.Collections.Generic;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class NavalCopyCrewLookupPatchesTests
{
    // The postfixes take their arguments by the vanilla parameter names, which only patching the real methods checks.
    [Fact]
    public void CopySkippingPatches_BindToTheTeamAiShipLookups()
    {
        var harmony = new Harmony("coop.tests.naval.copy_crew_lookup");
        try
        {
            Assert.NotEmpty(harmony.CreateClassProcessor(typeof(CopySkippingNearestShipPatch)).Patch());
            Assert.NotEmpty(harmony.CreateClassProcessor(typeof(CopySkippingConnectedShipPatch)).Patch());
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    // Vanilla's choice without copies: the stepped hull itself first, else the last own hull of its island, else none.
    [Fact]
    public void PickConnectedShip_PrefersTheSteppedHull_ElseTheLastOwnIslandHull_AndNeverACopy()
    {
        const ulong stepped = 0b0001, roped = 0b0010, alsoRoped = 0b0100, apart = 0b1000;
        const ulong island = stepped | roped | alsoRoped;
        var steppedHull = Ship(stepped, island);
        var ropedHull = Ship(roped, island);
        var alsoRopedHull = Ship(alsoRoped, island);
        var apartHull = Ship(apart, apart);
        var teamShips = new[] { ropedHull, steppedHull, alsoRopedHull, apartHull };
        var copies = new HashSet<MissionShip>(ReferenceEqualityComparer.Instance);

        Assert.Same(steppedHull, CopySkippingConnectedShipPatch.PickConnectedShip(teamShips, stepped, copies.Contains));

        copies.Add(steppedHull);
        Assert.Same(alsoRopedHull, CopySkippingConnectedShipPatch.PickConnectedShip(teamShips, stepped, copies.Contains));

        copies.Add(ropedHull);
        copies.Add(alsoRopedHull);
        Assert.Null(CopySkippingConnectedShipPatch.PickConnectedShip(teamShips, stepped, copies.Contains));
    }

    // A constructor-skipped hull with only its island bits set.
    private static MissionShip Ship(ulong uniqueId, ulong islandId)
    {
        var ship = ShipTestHulls.Uninitialized<MissionShip>();
        ship.ShipUniqueBitwiseID = uniqueId;
        ship.ShipIslandCombinedID = islandId;
        return ship;
    }
}
#endif
