#if DEBUG
using HarmonyLib;
using Missions.Battles;
using Missions.Naval;
using NavalDLC.Missions;
using NavalDLC.Missions.MissionLogics;
using NavalDLC.Missions.Objects;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;
using Xunit;
using static Coop.Tests.Missions.Battles.NavalLabTestShells;

namespace Coop.Tests.Missions.Battles;

[Collection("Mission.Current")]
public sealed class NavalLabFleetTests : IDisposable
{
    private readonly Harmony harmony = new("coop.tests.naval.fleet");

    // MissionShip's static sound ids resolve through the engine; reflection on its fields runs that initializer.
    public NavalLabFleetTests() => harmony.Patch(AccessTools.Method(typeof(SoundEvent), nameof(SoundEvent.GetEventIdFromString)),
        prefix: new HarmonyMethod(typeof(NavalLabFleetTests), nameof(SoundId)));

    public void Dispose() => harmony.UnpatchAll(harmony.Id);

    private static bool SoundId(ref int __result) { __result = 0; return false; }

    private static NavalLabManifest Manifest(int hulls)
    {
        var id = Guid.NewGuid();
        return new NavalLabManifest("naval-lab:" + id.ToString("N"), id, new[] { "A", "B" },
            Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToArray(),
            Enumerable.Range(0, 2 * hulls).Select(_ => Guid.NewGuid()).ToArray(), NavalLabMode.TwoClientNative);
    }

    private static NavalLabBehavior Fixture(NavalLabManifest manifest, string own)
    {
        var behavior = new NavalLabBehavior(manifest, own, null!, null!);
        AccessTools.PropertySetter(typeof(MissionBehavior), nameof(MissionBehavior.Mission)).Invoke(behavior, new object[] { Shell<Mission>() });
        behavior.Ships = manifest.Ships.Select(_ => Shell<MissionShip>()).ToArray();
        behavior.Agents = manifest.Combatants.Select(_ => Shell<Agent>()).ToArray();
        return behavior;
    }

    [Fact]
    public void Manifest_DefaultSingleHullKeepsParticipantSlots()
    {
        var manifest = Manifest(1);
        Assert.Equal(1, manifest.HullsPerParticipant);
        Assert.Equal(new[] { 0, 1 }, manifest.ShipOwners);
        Assert.True(manifest.IsFlagship(0) && manifest.IsFlagship(1));
        Assert.Equal(new[] { "A", "B" }, Enumerable.Range(0, 2).Select(manifest.ShipController));
    }

    [Fact]
    public void Manifest_TwoHullsAppendSecondaryHullsOwnedBySlotModuloParticipants()
    {
        var manifest = Manifest(2);
        Assert.Equal(2, manifest.HullsPerParticipant);
        Assert.Equal(new[] { 0, 1, 0, 1 }, manifest.ShipOwners);
        Assert.Equal(new[] { "A", "B", "A", "B" }, Enumerable.Range(0, 4).Select(manifest.ShipController));
        Assert.False(manifest.IsFlagship(2) || manifest.IsFlagship(3) || manifest.IsFlagship(-1));
        Assert.Equal(10, manifest.Combatants.Length);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(6)]
    public void Manifest_RejectsShipCountsThatAreNotOneOrTwoPerParticipant(int count)
    {
        var id = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => new NavalLabManifest("naval-lab:" + id.ToString("N"), id, new[] { "A", "B" },
            Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToArray(),
            Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToArray(), NavalLabMode.TwoClientNative));
    }

    [Fact]
    public void Manifest_RejectsRepeatedSecondaryHullIds()
    {
        var id = Guid.NewGuid();
        var ship = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => new NavalLabManifest("naval-lab:" + id.ToString("N"), id, new[] { "A", "B" },
            Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToArray(),
            new[] { Guid.NewGuid(), Guid.NewGuid(), ship, ship }, NavalLabMode.TwoClientNative));
    }

    [Theory]
    [InlineData("A", new[] { 0, 2 })]
    [InlineData("B", new[] { 1, 3 })]
    public void Behavior_OwnsAllOfItsParticipantsHulls_AndSizesPerHullCounters(string own, int[] owned)
    {
        var behavior = Fixture(Manifest(2), own);
        Assert.Equal(owned, Enumerable.Range(0, 4).Where(behavior.OwnsFactoryHull));
        Assert.Equal(owned, Enumerable.Range(0, 4).Where(behavior.FactoryBodyExpectedActive));
        Assert.Equal(new[] { owned[1] }, Enumerable.Range(0, 4).Where(behavior.IsOwnedSecondaryHull));
        Assert.Equal(4, behavior.shipSentSequences.Length);
        Assert.Equal(4, behavior.foreignSailStates.Length);
        Assert.Equal(owned[0], behavior.OwnSlot);
    }

    [Fact]
    public void Behavior_SingleHullCountersStayParticipantSized()
    {
        var behavior = Fixture(Manifest(1), "A");
        Assert.Equal(2, behavior.shipSentSequences.Length);
        Assert.Empty(Enumerable.Range(0, 2).Where(behavior.IsSecondaryHull));
    }

    [Fact]
    public void Layout_KeepsFlagshipRowAndPlacesSecondaryHullsOutsideHookRangeOfTheOtherParticipant()
    {
        var single = Fixture(Manifest(1), "A");
        Assert.Equal((250f, 250f), (single.HullOrigin(0).x, single.HullOrigin(0).y));
        Assert.Equal((274f, 250f), (single.HullOrigin(1).x, single.HullOrigin(1).y));
        var fleet = Fixture(Manifest(2), "A");
        var origins = Enumerable.Range(0, 4).Select(fleet.HullOrigin).ToArray();
        Assert.Equal((226f, 202f), (origins[2].x, origins[2].y));
        Assert.Equal((298f, 202f), (origins[3].x, origins[3].y));
        for (int a = 0; a < 4; a++)
            for (int b = a + 1; b < 4; b++)
                if (a % 2 != b % 2 && !(a < 2 && b < 2)) Assert.True(origins[a].AsVec2.Distance(origins[b].AsVec2) > 40f);
    }

    [Fact]
    public void FleetRowers_StayOutOfAgentsAuthorityAndStationMovement()
    {
        var manifest = Manifest(2);
        var behavior = Fixture(manifest, "A");
        var rower = Shell<Agent>();
        behavior.fleetRowers.Add(rower);
        var adapter = new NavalMissionAdapter { behavior = behavior };
        Assert.Equal(manifest.Combatants.Length, adapter.Agents.Length);
        Assert.DoesNotContain(rower, adapter.Agents);
        Assert.All(manifest.Combatants.Append(Guid.Empty), id =>
        {
            Assert.False(behavior.IsCommittedOarMovement(manifest.IncarnationId, id, rower));
            Assert.False(behavior.IsOccupiedHelmMovement(manifest.IncarnationId, id, rower));
        });
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    public void DetachmentSuppression_AppliesToFlagshipsOnly(int slot, bool suppressed)
    {
        var behavior = Fixture(Manifest(2), "A");
        var ship = behavior.Ships[slot];
        var logic = Shell<NavalShipsLogic>();
        AccessTools.PropertySetter(typeof(MissionBehavior), nameof(MissionBehavior.Mission)).Invoke(logic, new object[] { behavior.Mission });
        Set(ship, "<ShipsLogic>k__BackingField", logic);
        Set(ship, "<ShipOrigin>k__BackingField", new NavalLabShipOrigin(null!, _ => { }));
        var order = Shell<ShipOrder>();
        Set(order, "_ownerShip", ship);
        Assert.Equal(suppressed, behavior.OwnsFixedStationOrder(order));
    }

    [Theory]
    [InlineData(0, "rejected:not_owned_secondary_hull")]
    [InlineData(3, "rejected:not_owned_secondary_hull")]
    [InlineData(2, "rejected:native_controls_not_ready")]
    public void FleetOrder_RequiresAnOwnedSecondaryHullAndReadyControls(int slot, string expected)
    {
        var behavior = Fixture(Manifest(2), "A");
        Assert.Equal(expected, behavior.RequestFleetOrder(slot, follow: true));
    }

    [Fact]
    public void InspectFleet_ListsOnlyOwnedSecondaryHulls()
    {
        var behavior = Fixture(Manifest(2), "B");
        var fleet = JObject.FromObject(behavior.InspectFleet());
        Assert.Equal(2, (int)fleet["HullsPerParticipant"]!);
        Assert.Equal(new[] { 0, 1, 0, 1 }, fleet["shipOwners"]!.ToObject<int[]>());
        var hull = Assert.Single((JArray)fleet["ownedSecondaryHulls"]!);
        Assert.Equal(3, (int)hull["slot"]!);
        Assert.NotNull(hull["error"] ?? hull["unavailable"] ?? hull["movementOrder"]);
        Assert.Equal(20f, behavior.FleetFollowOffset(3));
        Assert.Equal(-20f, behavior.FleetFollowOffset(2));
    }
}
#endif
