#if DEBUG
using HarmonyLib;
using Missions.Messages;
using Missions.Naval;
using Moq;
using NavalDLC;
using NavalDLC.Missions.MissionLogics;
using NavalDLC.Missions.Objects;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>R41a-2: which points the naval engine seats a puppet on. A null agent shows whether the use was reached.</summary>
public class NavalShipEngineTests
{
    // W2: re-seating a puppet onto a picked-up item bound it to a freed object, a native AV.
    [Fact]
    public void ApplyStationUse_NeverSeatsOnAnItemOrARemovedPoint()
    {
        var engine = new NavalShipEngine(null!, null!, null!);

        engine.ApplyStationUse(null!, ShipTestHulls.CreateWorldItem(), inUse: true);
        engine.ApplyStationUse(null!, ShipTestHulls.CreatePoint(), inUse: true);
    }

    // A machine's deactivation is recomputed from local state and can lag its owner's, so the owner's use still seats it.
    [Fact]
    public void ApplyStationUse_StillSeatsOnADeactivatedMachine()
    {
        var engine = new NavalShipEngine(null!, null!, null!);
        var point = ShipTestHulls.CreatePoint();
        AccessTools.Field(typeof(UsableMissionObject), "_isDeactivated").SetValue(point, true);
        AccessTools.Field(typeof(ScriptComponentBehavior), "_gameEntity").SetValue(point, ValidEntity());

        Assert.Throws<NullReferenceException>(() => engine.ApplyStationUse(null!, point, inUse: true));
    }

    // R45 roll: the owner sends vanilla's crew centre in its body frame, so a copy at the same pose applies it at the same world point.
    [Fact]
    public void CrewLoadOf_IsVanillasCrewCentreInTheBodyFrame()
    {
        var rotation = Mat3.Identity;
        rotation.RotateAboutUp(1.2f);
        rotation.RotateAboutForward(0.07f);
        var body = new MatrixFrame(rotation, new Vec3(100f, 50f, 2f));
        var first = new Vec3(101f, 48f, 3f);
        var second = new Vec3(98f, 55f, 3.5f);

        var load = NavalShipEngine.CrewLoadOf(160f, (80f * first) + (80f * second), body, agentAboard: true);

        var centre = body.TransformToParent(load.LocalCentre);
        Assert.Equal(160f, load.Mass);
        Assert.Equal(99.5f, centre.x, 3);
        Assert.Equal(51.5f, centre.y, 3);
        Assert.Equal(3.25f, centre.z, 3);
    }

    // Nobody aboard (the crew boarded an enemy hull): a known-empty deck, so copies take no crew weight.
    [Fact]
    public void CrewLoadOf_IsEmptyWithNobodyAboard()
    {
        Assert.Same(BattleShipCrewLoad.Empty, NavalShipEngine.CrewLoadOf(0f, Vec3.Zero, MatrixFrame.Identity, agentAboard: false));
    }

    // Agents aboard but no mass committed is a frame vanilla skipped (dt 0), so it sends no load and copies keep the last.
    [Fact]
    public void CrewLoadOf_IsUnknownWithoutCommittedMassWhileAgentsStandAboard()
    {
        Assert.Null(NavalShipEngine.CrewLoadOf(0f, Vec3.Zero, MatrixFrame.Identity, agentAboard: true));
    }

    // Capture P0 2.1: an enemy copy's helm opens to the player while its formation has no unit, as vanilla opens an emptied
    // enemy helm; an own-side copy, a filled formation or a mission without a player team keep the late hull patch's setting.
    [Theory]
    [InlineData(true, false, 0, true)]
    [InlineData(true, true, 0, false)]
    [InlineData(true, false, 3, false)]
    [InlineData(false, false, 0, false)]
    public void OpensHelmToPlayer_OnlyForAnEmptyEnemyCopy(bool hasPlayerTeam, bool isPlayerSide, int formationUnits, bool expected)
    {
        Assert.Equal(expected, NavalShipEngine.OpensHelmToPlayer(hasPlayerTeam, isPlayerSide, formationUnits));
    }

    // C2: vanilla's capture swap points the old owner's crew record at the captor's orphan, which has no team, so those
    // swimmers are no crew of the orphan and never withdraw with it; a formation member always is crew.
    [Theory]
    [InlineData(true, true, false, true)]
    [InlineData(true, false, false, false)]
    [InlineData(false, false, true, true)]
    [InlineData(false, true, false, false)]
    public void IsHullCrew_NeedsTheHullsFormation_OrTrackingOnItsTeam(bool tracked, bool onHullsTeam, bool inFormation, bool expected)
    {
        Assert.Equal(expected, NavalShipEngine.IsHullCrew(tracked, onHullsTeam, inFormation));
    }

    // R47 bug A: vanilla's RemoveShip finds a hull only through its assignment and a capture's orphan holds none, so the orphan
    // borrows the first free one and vanilla's own lookup then finds it there.
    [Fact]
    public void LendFreeAssignment_LetsVanillasLookupFindAnOrphan()
    {
        var shipsLogic = ShipsLogicWith(out var assignments);
        var prize = CreateShip();
        assignments[0, 0].SetMissionShip(prize);
        var orphan = CreateShip();
        Assert.False(shipsLogic.FindAssignmentOfShipOrigin(orphan.ShipOrigin, out _));

        var lent = NavalShipEngine.LendFreeAssignment(assignments, orphan);

        Assert.NotSame(assignments[0, 0], lent);
        Assert.True(shipsLogic.FindAssignmentOfShipOrigin(orphan.ShipOrigin, out var found));
        Assert.Same(lent, found);
        Assert.Same(orphan, found.MissionShip);
        Assert.Same(prize, assignments[0, 0].MissionShip);
    }

    // With every assignment taken nothing is lent, so the removal refuses instead of overwriting another hull's slot.
    [Fact]
    public void LendFreeAssignment_LendsNothingWhenEveryAssignmentIsTaken()
    {
        ShipsLogicWith(out var assignments);
        foreach (var assignment in assignments)
            assignment.SetMissionShip(CreateShip());

        Assert.Null(NavalShipEngine.LendFreeAssignment(assignments, CreateShip()));
    }

    // R47 review: an orphan goes through a lent slot, a held hull through its own, and a hull whose origin another hull holds
    // isn't removed at all, since vanilla would take that other hull instead.
    [Fact]
    public void RemovalPathOf_TellsAHeldHullAnOrphanAndAHullWhoseOriginAnotherHullHolds()
    {
        var shipsLogic = ShipsLogicWith(out var assignments);
        var held = CreateShip();
        assignments[1, 2].SetMissionShip(held);
        var twin = CreateShip();
        twin.ShipOrigin = held.ShipOrigin;

        Assert.Equal(HullRemovalPath.Assigned, NavalShipEngine.RemovalPathOf(shipsLogic, held));
        Assert.Equal(HullRemovalPath.Unassigned, NavalShipEngine.RemovalPathOf(shipsLogic, CreateShip()));
        Assert.Equal(HullRemovalPath.OriginHeldByAnotherHull, NavalShipEngine.RemovalPathOf(shipsLogic, twin));
    }

    // Vanilla's RemoveShipAux finds the orphan through the lent slot, removes it and clears the slot.
    [Fact]
    public void RemoveThroughLentAssignment_RemovesTheOrphanThroughVanillasLookup_AndLeavesNoSlotHoldingIt()
    {
        var shipsLogic = ShipsLogicWith(out var assignments);
        var orphan = CreateShip();

        var outcome = NavalShipEngine.RemoveThroughLentAssignment(assignments, orphan, ship => VanillaRemoveShip(shipsLogic, ship));

        Assert.Equal(LentRemoval.Removed, outcome);
        Assert.True(orphan.IsRemoved);
        Assert.All(assignments.Cast<ShipAssignment>(), assignment => Assert.Null(assignment.MissionShip));
    }

    // A removal handler that throws inside vanilla's removal must not leave the orphan counted in the lent slot.
    [Fact]
    public void RemoveThroughLentAssignment_FreesTheSlot_WhenTheRemovalThrows()
    {
        ShipsLogicWith(out var assignments);
        var orphan = CreateShip();

        Assert.Throws<InvalidOperationException>(() => NavalShipEngine.RemoveThroughLentAssignment(assignments, orphan,
            _ => throw new InvalidOperationException("a BeforeShipRemovedEvent handler threw")));

        Assert.All(assignments.Cast<ShipAssignment>(), assignment => Assert.Null(assignment.MissionShip));
    }

    // A removal that doesn't take the hull reads as not removed and frees the slot too.
    [Fact]
    public void RemoveThroughLentAssignment_ReportsAHullVanillaKept_AndFreesTheSlot()
    {
        ShipsLogicWith(out var assignments);
        var orphan = CreateShip();

        var outcome = NavalShipEngine.RemoveThroughLentAssignment(assignments, orphan, _ => { });

        Assert.Equal(LentRemoval.NotRemoved, outcome);
        Assert.All(assignments.Cast<ShipAssignment>(), assignment => Assert.Null(assignment.MissionShip));
    }

    // With every slot taken nothing is lent and vanilla's removal never runs.
    [Fact]
    public void RemoveThroughLentAssignment_RunsNoRemoval_WithoutAFreeSlot()
    {
        ShipsLogicWith(out var assignments);
        foreach (var assignment in assignments)
            assignment.SetMissionShip(CreateShip());
        bool ran = false;

        var outcome = NavalShipEngine.RemoveThroughLentAssignment(assignments, CreateShip(), _ => ran = true);

        Assert.Equal(LentRemoval.NoFreeAssignment, outcome);
        Assert.False(ran);
    }

    // AmmoCount leaves out the loaded bolt and a reload spends its bolt only as it ends, so a swap mid-reload gives one bolt to
    // the fresh weapon's load instead of adding one.
    [Theory]
    [InlineData(22, true, 22)]
    [InlineData(22, false, 21)]
    [InlineData(0, false, 0)]
    public void CarriedAmmo_KeepsTheTotalBoltsAcrossASwap(int ammoCount, bool isLoaded, int expected)
    {
        Assert.Equal(expected, NavalShipEngine.CarriedAmmo(ammoCount, isLoaded));
    }

    // NavalShipsLogic.RemoveShip and RemoveShipAux's bookkeeping, without the scene: find the assignment, clear it, mark removed.
    private static void VanillaRemoveShip(NavalShipsLogic shipsLogic, MissionShip ship)
    {
        if (!shipsLogic.FindAssignmentOfShipOrigin(ship.ShipOrigin, out var assignment)) return;

        assignment.RemoveShip();
        assignment.Clear();
        ship._isRemoved = true;
    }

    // A constructor-skipped NavalShipsLogic with vanilla's empty 3 x 11 assignment table.
    private static NavalShipsLogic ShipsLogicWith(out ShipAssignment[,] assignments)
    {
        var shipsLogic = (NavalShipsLogic)RuntimeHelpers.GetUninitializedObject(typeof(NavalShipsLogic));
        assignments = new ShipAssignment[3, 11];
        for (int side = 0; side < 3; side++)
        {
            for (int index = 0; index < 11; index++)
                assignments[side, index] = ShipAssignment.Create((TeamSideEnum)side, (FormationClass)index);
        }

        AccessTools.Field(typeof(NavalShipsLogic), "_shipAssignments").SetValue(shipsLogic, assignments);
        return shipsLogic;
    }

    // A hull with its own origin and ship object, the two an assignment needs to count as holding it.
    private static MissionShip CreateShip()
    {
        var ship = ShipTestHulls.Uninitialized<MissionShip>();
        ship.ShipOrigin = new Mock<IShipOrigin>().Object;
        ship._missionShipObject = (MissionShipObject)RuntimeHelpers.GetUninitializedObject(typeof(MissionShipObject));
        return ship;
    }

    // WeakGameEntity.IsValid only compares the pointer with zero; nothing here dereferences it.
    internal static WeakGameEntity ValidEntity() =>
        (WeakGameEntity)AccessTools.Constructor(typeof(WeakGameEntity), new[] { typeof(UIntPtr) }).Invoke(new object[] { new UIntPtr(1) });
}
#endif
