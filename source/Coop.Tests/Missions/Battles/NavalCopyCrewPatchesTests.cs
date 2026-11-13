#if COOP_NAVAL
using Autofac;
using GameInterface;
using HarmonyLib;
using Missions.Naval;
using Moq;
using NavalDLC.Missions.MissionLogics;
using NavalDLC.Missions.Objects;
using System.Collections.Generic;
using TaleWorlds.Core;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class NavalCopyCrewPatchesTests
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

    // Vanilla's crew share over a team holding two own hulls and a copy, as the battle host's ally team does with an allied AI
    // fleet next to another player's copy: the whole share goes to the own hulls, and the copy is back in its place untouched.
    [Fact]
    public void CrewShare_SpreadsTheTeamsTroopsOverItsOwnHullsOnly()
    {
        var own = HullAgents(crewCapacity: 20);
        var copy = HullAgents(crewCapacity: 20);
        var alsoOwn = HullAgents(crewCapacity: 20);
        var team = new NavalTeamAgents(null, BattleSideEnum.Defender, TeamSideEnum.PlayerAllyTeam);
        team._allShipAgents.AddRange(new[] { own, copy, alsoOwn });
        for (int troop = 0; troop < 30; troop++) team._allTroopOrigins.Add(Mock.Of<IAgentOriginBase>());
        var registry = new Mock<INavalForeignHullRegistry>();
        registry.Setup(hulls => hulls.Contains(copy.Ship)).Returns(true);
        var builder = new ContainerBuilder();
        builder.RegisterInstance(registry.Object).As<INavalForeignHullRegistry>();
        using var container = builder.Build();
        var harmony = new Harmony("coop.tests.naval.copy_crew_share");
        var activeUse = ContainerProvider.UseContainerThreadSafe(container);
        try
        {
            Assert.NotEmpty(harmony.CreateClassProcessor(typeof(CopySkippingCrewSharePatch)).Patch());

            team.AutoComputeDesiredTroopCountsPerShip(loadBalanceShips: true, troopLimitFromBattleSize: 100);

            Assert.Equal(new[] { own, copy, alsoOwn }, team._allShipAgents);
            Assert.Equal((15, 15, 20), (own.DesiredTroopCount, alsoOwn.DesiredTroopCount, copy.DesiredTroopCount));
        }
        finally
        {
            // Clears while the handle is still held, as NavalClientContainer.Dispose does.
            ContainerProvider.Clear();
            activeUse.Dispose();
            harmony.UnpatchAll(harmony.Id);
        }
    }

    // A constructor-skipped hull's crew record wanting its full crew, as vanilla's spawn makes it.
    private static NavalShipAgents HullAgents(int crewCapacity)
    {
        var origin = new Mock<IShipOrigin>();
        origin.SetupGet(ship => ship.TotalCrewCapacity).Returns(crewCapacity);
        origin.SetupGet(ship => ship.SkeletalCrewCapacity).Returns(crewCapacity / 2);
        var hull = ShipTestHulls.Uninitialized<MissionShip>();
        hull.ShipOrigin = origin.Object;
        var agents = ShipTestHulls.Uninitialized<NavalShipAgents>();
        agents.Ship = hull;
        agents.DesiredTroopCount = crewCapacity;
        return agents;
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
