#if COOP_NAVAL
using GameInterface.Policies;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Missions.Naval;
using Moq;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class CoopShipSnapshotBuilderTests
{
    [Fact]
    public void Build_KeepsTheOwnerWithoutJoiningThePartyShips()
    {
        var party = CreateParty();
        var source = CreateOwnedShip(party, hitPoints: 600f);

        var snapshot = new CoopShipSnapshotBuilder(Mock.Of<IObjectManager>()).Build(source);

        Assert.Same(party, snapshot.Owner);
        Assert.Equal(new[] { source }, party.Ships);
    }

    [Fact]
    public void Build_CopiesDamageStateWithoutSharingIt()
    {
        var source = CreateOwnedShip(CreateParty(), hitPoints: 600f);

        var snapshot = new CoopShipSnapshotBuilder(Mock.Of<IObjectManager>()).Build(source);
        snapshot.HitPoints = 100f;

        Assert.Equal(source.RandomValue, snapshot.RandomValue);
        Assert.Equal(600f, source.HitPoints);
        Assert.Equal(100f, snapshot.HitPoints);
    }

    [Fact]
    public void Build_MarksOnlyTheSnapshotForTheCampaignGates()
    {
        var source = CreateOwnedShip(CreateParty(), hitPoints: 600f);

        var snapshot = new CoopShipSnapshotBuilder(Mock.Of<IObjectManager>()).Build(source);

        Assert.True(CoopShipSnapshots.Contains(snapshot));
        Assert.False(CoopShipSnapshots.Contains(source));
        Assert.False(CoopShipSnapshots.Contains(null));
    }

    [Fact]
    public void Build_RunsWithOriginalsAllowedSoTheSnapshotStaysOffTheShipSyncPatches()
    {
        var source = CreateOwnedShip(CreateParty(), hitPoints: 600f);
        var harmony = new Harmony("coop.tests.naval.ship_snapshot_builder");
        try
        {
            // Read by the constructor's HitPoints write; its foreach keeps the JIT from inlining it.
            harmony.Patch(
                AccessTools.PropertyGetter(typeof(Ship), nameof(Ship.MaxHitPoints)),
                postfix: new HarmonyMethod(typeof(CoopShipSnapshotBuilderTests), nameof(RecordOriginalsAllowed)));
            recordedOriginalsAllowed = new List<bool>();

            new CoopShipSnapshotBuilder(Mock.Of<IObjectManager>()).Build(source);

            Assert.NotEmpty(recordedOriginalsAllowed);
            Assert.All(recordedOriginalsAllowed, allowed => Assert.True(allowed));
            Assert.False(CallOriginalPolicy.AreOriginalsAllowedForCurrentOperation);
        }
        finally
        {
            recordedOriginalsAllowed = null;
            harmony.UnpatchAll(harmony.Id);
        }
    }

    [Fact]
    public void Build_CopiesTheHitPointsWithoutRunningTheirSettersOnTheOwnedSnapshot()
    {
        var source = CreateOwnedShip(CreateParty(), hitPoints: 600f);
        source.SailHitPoints = 200f;
        var harmony = new Harmony("coop.tests.naval.ship_snapshot_builder_setters");
        try
        {
            // The campaign HitPoints postfix only acts on a ship that has an owner, which the snapshot has once built.
            var probe = new HarmonyMethod(typeof(CoopShipSnapshotBuilderTests), nameof(RecordOwnedSetter));
            harmony.Patch(AccessTools.PropertySetter(typeof(Ship), nameof(Ship.HitPoints)), postfix: probe);
            harmony.Patch(AccessTools.PropertySetter(typeof(Ship), nameof(Ship.SailHitPoints)), postfix: probe);
            recordedOwnedSetters = new List<Ship>();

            var snapshot = new CoopShipSnapshotBuilder(Mock.Of<IObjectManager>()).Build(source);

            Assert.Empty(recordedOwnedSetters);
            Assert.Equal(600f, snapshot.HitPoints);
            Assert.Equal(200f, snapshot.SailHitPoints);
        }
        finally
        {
            recordedOwnedSetters = null;
            harmony.UnpatchAll(harmony.Id);
        }
    }

    // M6 D1: an own snapshot names its party ship by registry id, so the fate report can address the campaign ship.
    [Fact]
    public void Build_RecordsTheSourceShipsRegistryId()
    {
        var source = CreateOwnedShip(CreateParty(), hitPoints: 600f);
        var unregistered = CreateOwnedShip(CreateParty(), hitPoints: 600f);
        var objectManager = new Mock<IObjectManager>();
        string sourceId = "Ship_3";
        objectManager.Setup(manager => manager.TryGetId(source, out sourceId)).Returns(true);
        var builder = new CoopShipSnapshotBuilder(objectManager.Object);

        var own = builder.Build(source);
        var ofUnregistered = builder.Build(unregistered);

        Assert.Equal("Ship_3", CoopShipSnapshots.CampaignShipIdOf(own));
        Assert.Null(CoopShipSnapshots.CampaignShipIdOf(ofUnregistered));
        Assert.Null(CoopShipSnapshots.CampaignShipIdOf(source));
        Assert.Null(CoopShipSnapshots.CampaignShipIdOf(null));
    }

    [ThreadStatic]
    private static List<Ship> recordedOwnedSetters;

    private static void RecordOwnedSetter(Ship __instance)
    {
        if (__instance.Owner != null) recordedOwnedSetters?.Add(__instance);
    }

    [ThreadStatic]
    private static List<bool> recordedOriginalsAllowed;

    private static void RecordOriginalsAllowed() =>
        recordedOriginalsAllowed?.Add(CallOriginalPolicy.AreOriginalsAllowedForCurrentOperation);

    private static PartyBase CreateParty()
    {
        var party = (PartyBase)RuntimeHelpers.GetUninitializedObject(typeof(PartyBase));
        party._ships = new MBList<Ship>();
        return party;
    }

    private static Ship CreateOwnedShip(PartyBase owner, float hitPoints)
    {
        var hull = (ShipHull)RuntimeHelpers.GetUninitializedObject(typeof(ShipHull));
        hull.AvailableSlots = new MBReadOnlyDictionary<string, ShipSlot>(new System.Collections.Generic.Dictionary<string, ShipSlot>());
        typeof(ShipHull).GetProperty(nameof(ShipHull.MaxHitPoints))!.SetValue(hull, 1000);
        typeof(ShipHull).GetProperty(nameof(ShipHull.MaxSailHitPoints))!.SetValue(hull, 500);

        var ship = new Ship(hull) { Owner = owner };
        ship.HitPoints = hitPoints;
        return ship;
    }
}
#endif
