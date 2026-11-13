#if COOP_NAVAL
using Autofac;
using GameInterface;
using HarmonyLib;
using Missions.Naval;
using Moq;
using NavalDLC.Missions.MissionLogics;
using NavalDLC.Missions.Objects;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
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

    // Vanilla's crew share and troop assignment over a team holding two own hulls and a copy, as the battle host's ally team does
    // with an allied AI fleet next to another player's copy: every troop goes to the own hulls, and the copy is back in its place.
    [Fact]
    public void CrewShareAndAssignment_PutTheTeamsTroopsOnItsOwnHullsOnly()
    {
        var team = new NavalTeamAgents(
            (NavalAgentsLogic)RuntimeHelpers.GetUninitializedObject(typeof(NavalAgentsLogic)), BattleSideEnum.Defender,
            TeamSideEnum.PlayerAllyTeam);
        var own = HullAgents(team, crewCapacity: 20);
        var copy = HullAgents(team, crewCapacity: 20);
        var alsoOwn = HullAgents(team, crewCapacity: 20);
        team._allShipAgents.AddRange(new[] { own, copy, alsoOwn });
        var soldier = Mock.Of<BasicCharacterObject>();
        for (int troop = 0; troop < 30; troop++)
            team.AddTroopOrigin(Mock.Of<IAgentOriginBase>(origin =>
                origin.Troop == soldier && origin.GetTraitsMask() == TroopTraitsMask.Melee));
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
            team.AssignTroops();

            Assert.Equal(new[] { own, copy, alsoOwn }, team._allShipAgents);
            Assert.Equal((15, 15, 20), (own.DesiredTroopCount, alsoOwn.DesiredTroopCount, copy.DesiredTroopCount));
            Assert.Equal((15, 15, 0), (own.ReservedTroopsCount, alsoOwn.ReservedTroopsCount, copy.ReservedTroopsCount));
        }
        finally
        {
            // Clears while the handle is still held, as NavalClientContainer.Dispose does.
            ContainerProvider.Clear();
            activeUse.Dispose();
            harmony.UnpatchAll(harmony.Id);
        }
    }

    // A constructor-skipped hull's crew record of the team, wanting its full crew under vanilla's default melee-and-ranged filter.
    private static NavalShipAgents HullAgents(NavalTeamAgents team, int crewCapacity)
    {
        var origin = new Mock<IShipOrigin>();
        origin.SetupGet(ship => ship.TotalCrewCapacity).Returns(crewCapacity);
        origin.SetupGet(ship => ship.SkeletalCrewCapacity).Returns(crewCapacity / 2);
        var hull = ShipTestHulls.Uninitialized<MissionShip>();
        hull.ShipOrigin = origin.Object;
        var agents = ShipTestHulls.Uninitialized<NavalShipAgents>();
        agents.Ship = hull;
        agents.DesiredTroopCount = crewCapacity;
        agents.TroopClassFilter = TroopTraitsMask.Melee | TroopTraitsMask.Ranged;
        // The readonly collections and team the skipped constructor would have set.
        SetField(agents, nameof(NavalShipAgents._teamAgents), team);
        SetField(agents, nameof(NavalShipAgents._activeAgents), new MBList<Agent>());
        SetField(agents, nameof(NavalShipAgents._reservedTroops), new Dictionary<IAgentOriginBase, NavalTroopAssignment>());
        SetField(agents, nameof(NavalShipAgents._reservedOrderedTroops), new MBSortedMultiList<int, NavalTroopAssignment>(true));
        return agents;
    }

    // Sets a readonly field the publicizer can't make writable.
    private static void SetField(NavalShipAgents agents, string name, object value) =>
        AccessTools.Field(typeof(NavalShipAgents), name).SetValue(agents, value);

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
