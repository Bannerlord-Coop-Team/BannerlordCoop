#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using Missions.Battles;
using Missions.Naval;
using NavalDLC.Missions;
using NavalDLC.Missions.MissionLogics;
using NavalDLC.Missions.Objects;
using NavalDLC.Missions.Objects.UsableMachines;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

[Collection("Mission.Current")]
public sealed class NavalLabTwoClientNativeTests : IDisposable
{
    private readonly Harmony harmony = new("coop.tests.naval.two-native");
    private readonly MissionCurrentScope scope = new();
    private static int stopped;
    private static int assigned;
    private static bool weaponTarget;
    private static Dictionary<string, ShipOarMachine> stationInventory = null!;
    private static int uses;
    private static int spawnCallbacks;
    private static bool throwOnSecondUse;
    private static Agent localMain = null!;
    public NavalLabTwoClientNativeTests()
    {
        stopped = assigned = 0;
        Patch(AccessTools.Method(typeof(SoundEvent), nameof(SoundEvent.GetEventIdFromString)), nameof(Zero));
        Patch(AccessTools.Method(typeof(MBAnimation), nameof(MBAnimation.GetActionCodeWithName)), nameof(Zero));
        Patch(AccessTools.PropertyGetter(typeof(Formation), nameof(Formation.CountOfDetachableNonPlayerUnits)), nameof(Zero));
        Patch(AccessTools.PropertyGetter(typeof(Agent), nameof(Agent.IsAIControlled)), nameof(True));
        Patch(AccessTools.Method(typeof(Agent), nameof(Agent.StopUsingGameObject)), nameof(Stop));
        Patch(AccessTools.Method(typeof(UsableMachine), nameof(UsableMachine.IsDisabledForBattleSideAI)), nameof(False));
        Patch(AccessTools.Method(typeof(UsableMachine), nameof(UsableMachine.AddAgentAtSlotIndex)), nameof(Assign));
        var placement = typeof(MissionShip).GetProperty(nameof(MissionShip.ShipPlacementDetachment))!.PropertyType;
        Patch(AccessTools.PropertyGetter(placement, "HasAgent"), nameof(False));
    }
    private void Patch(MethodBase method, string prefix) => harmony.Patch(method, prefix: new HarmonyMethod(GetType(), prefix));
    private static bool Zero(ref int __result) { __result = 0; return false; }
    private static bool True(ref bool __result) { __result = true; return false; }
    private static bool False(ref bool __result) { __result = false; return false; }
    private static bool Stop() { stopped++; return false; }
    private static bool Assign(UsableMachine __instance) { assigned++; weaponTarget = __instance is RangedSiegeWeapon; return false; }
    private static void Set(object instance, string field, object? value) => AccessTools.Field(instance.GetType(), field).SetValue(instance, value);
    private static T Shell<T>()
    {
        var managed = typeof(ScriptComponentBehavior).BaseType!.Assembly.GetType("TaleWorlds.DotNet.Managed")!;
        var field = managed.GetField("_moduleTypes", BindingFlags.Static | BindingFlags.NonPublic)!;
        var previous = field.GetValue(null);
        try
        {
            if (previous == null) field.SetValue(null, new Dictionary<string, Type>());
            return (T)FormatterServices.GetUninitializedObject(typeof(T));
        }
        finally { field.SetValue(null, previous); }
    }
    private NavalLabBehavior Fixture(NavalLabMode mode, string owner = "A")
    {
        var id = Guid.NewGuid();
        int count = mode == NavalLabMode.SingleClientNative ? 1 : 2;
        var fixture = new NavalLabBehavior(new NavalLabManifest("naval-lab:" + id.ToString("N"), id,
            new[] { "A", "B" }.Take(count).ToArray(), Enumerable.Range(0, count * 5).Select(_ => Guid.NewGuid()).ToArray(),
            Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToArray(), mode), owner, null!, null!);
        AccessTools.PropertySetter(typeof(MissionBehavior), nameof(MissionBehavior.Mission)).Invoke(fixture, new object[] { scope.Instance });
        return fixture;
    }
    private ShipOrder Order(bool weapon)
    {
        var ship = Shell<MissionShip>();
        var formation = Shell<Formation>();
        var order = Shell<ShipOrder>();
        var logic = new NavalShipsLogic();
        AccessTools.PropertySetter(typeof(MissionBehavior), nameof(MissionBehavior.Mission)).Invoke(logic, new object[] { scope.Instance });
        Set(ship, "<ShipsLogic>k__BackingField", logic);
        Set(ship, "<ShipOrigin>k__BackingField", Shell<NavalLabShipOrigin>());
        Set(ship, "<Formation>k__BackingField", formation);
        Set(ship, "<ShipPlacementDetachment>k__BackingField", FormatterServices.GetUninitializedObject(
            typeof(MissionShip).GetProperty(nameof(MissionShip.ShipPlacementDetachment))!.PropertyType));
        Set(order, "_ownerShip", ship); Set(order, "_ownerFormation", formation); Set(order, "_navalShipsLogic", logic);
        Set(formation, "Team", Shell<Team>());
        Set(formation, "_detachments", new MBList<IDetachment> { Shell<ShipOarMachine>() });
        Set(ship, "_attachmentMachines", new MBList<ShipAttachmentMachine>());
        Set(ship, "_attachmentPointMachines", new MBList<ShipAttachmentPointMachine>());
        var helm = Shell<ShipControllerMachine>();
        Set(helm, "<PilotStandingPoint>k__BackingField", Shell<StandingPoint>());
        Set(ship, "<ShipControllerMachine>k__BackingField", helm);
        var oar = Shell<ShipOarMachine>(); var point = Shell<StandingPoint>();
        Set(oar, "<PilotStandingPoint>k__BackingField", point);
        Set(point, "_userAgent", Shell<Agent>());
        Set(ship, "_leftSideShipOarMachines", new MBList<ShipOarMachine> { oar });
        Set(ship, "_rightSideShipOarMachines", new MBList<ShipOarMachine>());
        if (weapon)
        {
            var machine = Shell<Ballista>();
            Set(machine, "<PilotStandingPoint>k__BackingField", Shell<StandingPoint>());
            Set(ship, "<ShipSiegeWeapon>k__BackingField", machine);
        }
        return order;
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StockAllocatorReassignsOarsman_ExactTwoClientGatePreventsIt(bool weapon)
    {
        var order = Order(weapon);
        NavalLabPhysicsPatches.Active = null;
        order.ManageShipDetachments();
        Assert.Equal(1, stopped); Assert.Equal(1, assigned); Assert.Equal(weapon, weaponTarget);
        Patch(AccessTools.Method(typeof(ShipOrder), nameof(ShipOrder.ManageShipDetachments)), "AllocatorGate");
        NavalLabPhysicsPatches.Active = Fixture(NavalLabMode.TwoClientNative);
        order.ManageShipDetachments();
        Assert.Equal(1, stopped); Assert.Equal(1, assigned);
        NavalLabPhysicsPatches.Active.factoryTerminal = true;
        order.ManageShipDetachments(); Assert.Equal(1, assigned);
        foreach (var mode in new[] { NavalLabMode.Activation, NavalLabMode.HeldHelm, NavalLabMode.SingleClientNative, NavalLabMode.FactoryAuthorityProbe })
        {
            NavalLabPhysicsPatches.Active = Fixture(mode);
            order.ManageShipDetachments();
        }
        Assert.Equal(5, assigned);
    }
    private static bool AllocatorGate(ShipOrder __instance)
    {
        var patch = typeof(NavalLabNativePatches).GetNestedType("FixedInitialOarAllocation", BindingFlags.NonPublic)!;
        return (bool)AccessTools.Method(patch, "Prefix").Invoke(null, new object[] { __instance })!;
    }
    [Theory]
    [InlineData("A", 0)]
    [InlineData("B", 1)]
    public void LocalCaptainAndShipAreSelectedByOwner_NotLastHull(string owner, int slot)
    {
        var fixture = Fixture(NavalLabMode.TwoClientNative, owner);
        fixture.Ships = new[] { Shell<MissionShip>(), Shell<MissionShip>() };
        fixture.Agents = Enumerable.Range(0, 10).Select(_ => Shell<Agent>()).ToArray();
        Assert.Same(fixture.Ships[slot], fixture.LocalShip);
        Assert.Same(fixture.Agents[slot * 5], fixture.LocalCaptain);
        Assert.Null(fixture.GetLocalControlledShip());
        Assert.False(fixture.CanUseNativeControls);
        Assert.Equal("rejected:owner_not_ready", fixture.CompleteNativeDeployment());
        Assert.False(fixture.nativeDeploymentAttempted);
        fixture.OnDeploymentFinished(); fixture.OnAfterDeploymentFinished();
        Assert.Equal(1, fixture.nativeDeploymentCallbacks); Assert.Equal(1, fixture.nativeAfterDeploymentCallbacks);
        Assert.False(fixture.CanUseNativeControls);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ActualPlayerControlledShipGetter_SelectsOwnedHelmOrNothing(bool second)
    {
        var fixture = Fixture(NavalLabMode.TwoClientNative, second ? "B" : "A");
        fixture.Ships = new[] { Shell<MissionShip>(), Shell<MissionShip>() };
        fixture.Agents = Enumerable.Range(0, 10).Select(_ => Shell<Agent>()).ToArray();
        localMain = fixture.LocalCaptain;
        Patch(AccessTools.PropertyGetter(typeof(Mission), nameof(Mission.MainAgent)), nameof(Main));
        Patch(AccessTools.PropertyGetter(typeof(Agent), nameof(Agent.IsPlayerControlled)), nameof(True));
        var helm = Shell<ShipControllerMachine>(); var point = Shell<StandingPoint>();
        Set(helm, "<PilotStandingPoint>k__BackingField", point);
        Set(fixture.LocalShip, "<ShipControllerMachine>k__BackingField", helm);
        Set(localMain, "<CurrentlyUsedGameObject>k__BackingField", point); Set(point, "_userAgent", localMain);
        var logic = new NavalShipsLogic();
        AccessTools.PropertySetter(typeof(MissionBehavior), nameof(MissionBehavior.Mission)).Invoke(logic, new object[] { scope.Instance });
        Set(logic, "<PlayerControlledShip>k__BackingField", fixture.Ships[1]);
        var patch = typeof(NavalLabNativePatches).GetNestedType("OwnerShipSelection", BindingFlags.NonPublic)!;
        harmony.Patch(AccessTools.PropertyGetter(typeof(NavalShipsLogic), nameof(NavalShipsLogic.PlayerControlledShip)),
            postfix: new HarmonyMethod(AccessTools.Method(patch, "Postfix")));
        NavalLabPhysicsPatches.Active = fixture;
        Assert.Null(logic.PlayerControlledShip);
        fixture.nativeDeploymentComplete = true;
        Assert.Same(fixture.LocalShip, logic.PlayerControlledShip);
        Set(localMain, "<CurrentlyUsedGameObject>k__BackingField", null);
        Assert.Null(logic.PlayerControlledShip);
        fixture.factoryTerminal = true;
        Assert.Null(logic.PlayerControlledShip);
    }
    private static bool Main(ref Agent __result) { __result = localMain; return false; }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("conflict")]
    [InlineData("foreign")]
    [InlineData("partial")]
    public void StationApplyUsesNativePairsOnce_AndNeverDisplacesOrRepairs(string condition)
    {
        var fixture = Fixture(NavalLabMode.TwoClientNative);
        Assert.Throws<InvalidOperationException>(() => fixture.CreateStations());
        Patch(AccessTools.Method(typeof(NavalLabBehavior), "StationInventory"), nameof(Inventory));
        Patch(AccessTools.Method(typeof(Agent), nameof(Agent.IsActive)), nameof(True));
        Patch(AccessTools.Method(typeof(Agent), nameof(Agent.UseGameObject)), nameof(Use));
        Patch(AccessTools.Method(typeof(ShipOarMachine), nameof(ShipOarMachine.OnPilotAssignedDuringSpawn)), nameof(Spawn));
        fixture.Ships = new[] { Shell<MissionShip>(), Shell<MissionShip>() };
        fixture.Agents = Enumerable.Range(0, 10).Select(_ => Shell<Agent>()).ToArray();
        var formation = Shell<Formation>(); Set(fixture.Ships[0], "<Formation>k__BackingField", formation);
        foreach (var actor in fixture.Agents.Take(5)) Set(actor, "_formation", formation);
        stationInventory = new Dictionary<string, ShipOarMachine>();
        foreach (var key in new[] { "a", "b", "c", "d" })
        {
            var machine = Shell<ShipOarMachine>(); var point = Shell<StandingPoint>();
            Set(machine, "<PilotStandingPoint>k__BackingField", point);
            var oarType = AccessTools.Field(typeof(ShipOarMachine), "_oar").FieldType;
            var oar = FormatterServices.GetUninitializedObject(oarType);
            Set(oar, "<OwnerShip>k__BackingField", fixture.Ships[0]); Set(machine, "_oar", oar);
            stationInventory.Add(key, machine);
        }
        uses = spawnCallbacks = 0; throwOnSecondUse = condition == "partial";
        var manifest = fixture.manifest;
        var value = new global::Missions.Messages.NetworkNavalLabStations(manifest.IncarnationId, 1, 0, "commit",
            manifest.Combatants.Skip(1).Take(4).ToArray(), stationInventory.Keys.ToArray());
        if (condition == "foreign")
        {
            Set(stationInventory["a"].PilotStandingPoint, "_userAgent", fixture.Agents[6]);
            Assert.Throws<InvalidOperationException>(() => fixture.ApplyStations(value)); Assert.Equal(0, uses); return;
        }
        if (condition == "partial")
        {
            Assert.Throws<InvalidOperationException>(() => fixture.ApplyStations(value));
            Assert.Equal(2, uses); Assert.Equal(1, spawnCallbacks);
            Assert.Throws<InvalidOperationException>(() => fixture.ApplyStations(value)); Assert.Equal(2, uses); return;
        }
        fixture.ApplyStations(value);
        Assert.Equal(4, uses); Assert.Equal(4, spawnCallbacks); Assert.True(fixture.ObserveStations(value));
        if (condition == "duplicate") fixture.ApplyStations(value);
        else Assert.Throws<InvalidOperationException>(() => fixture.ApplyStations(new global::Missions.Messages.NetworkNavalLabStations(
            manifest.IncarnationId, 1, 0, "commit", value.Combatants, value.Keys.Reverse().ToArray())));
        Assert.Equal(4, uses); Assert.Equal(4, spawnCallbacks);
    }
    private static bool Inventory(ref Dictionary<string, ShipOarMachine> __result) { __result = stationInventory; return false; }
    private static bool Use(Agent __instance, UsableMissionObject usedObject)
    {
        uses++;
        if (throwOnSecondUse && uses == 2) throw new InvalidOperationException("native use boundary failed");
        Set(__instance, "<CurrentlyUsedGameObject>k__BackingField", usedObject); Set(usedObject, "_userAgent", __instance); return false;
    }
    private static bool Spawn(ShipOarMachine __instance)
    { spawnCallbacks++; Set(__instance, "_isPilotSitting", true); Set(__instance, "_lastPilotAgent", __instance.PilotAgent); return false; }

    public void Dispose() { NavalLabPhysicsPatches.Active = null; harmony.UnpatchAll(harmony.Id); scope.Dispose(); }
}
#endif
