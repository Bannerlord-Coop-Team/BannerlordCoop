#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using Missions.Battles;
using Missions.Messages;
using Missions.Naval;
using NavalDLC.Missions.Objects;
using NavalDLC.Missions.Objects.UsableMachines;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

[Collection("Mission.Current")]
public sealed class NavalLabStationReleaseTests : IDisposable
{
    private static readonly string[] Keys = { "left0", "left1", "right0", "right1" };
    private readonly Harmony harmony = new("coop.tests.naval.station-release");
    private readonly MissionCurrentScope scope = new();
    private static Dictionary<string, ShipOarMachine> inventory = null!;
    private static int stops;
    private static int targetWrites;
    private readonly NavalLabBehavior behavior;
    private readonly List<NetworkNavalLabStations> published = new();

    public NavalLabStationReleaseTests()
    {
        stops = targetWrites = 0;
        // Agent's static initializer resolves action and sound ids through the engine.
        Patch(AccessTools.Method(typeof(SoundEvent), nameof(SoundEvent.GetEventIdFromString)), nameof(Zero));
        Patch(AccessTools.Method(typeof(MBAnimation), nameof(MBAnimation.GetActionCodeWithName)), nameof(Zero));
        Patch(AccessTools.Method(typeof(NavalLabBehavior), "StationInventory"), nameof(Inventory));
        Patch(AccessTools.Method(typeof(Agent), nameof(Agent.IsActive)), nameof(True));
        Patch(AccessTools.PropertyGetter(typeof(Agent), nameof(Agent.IsAIControlled)), nameof(True));
        Patch(AccessTools.PropertyGetter(typeof(Agent), nameof(Agent.Controller)), nameof(NoController));
        Patch(AccessTools.Method(typeof(Agent), nameof(Agent.StopUsingGameObject)), nameof(StopUse));
        Patch(AccessTools.Method(typeof(Agent), nameof(Agent.SetTargetPositionAndDirection)), nameof(TargetWrite));
        var id = Guid.NewGuid();
        behavior = new NavalLabBehavior(new NavalLabManifest("naval-lab:" + id.ToString("N"), id, new[] { "A", "B" },
            Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToArray(), new[] { Guid.NewGuid(), Guid.NewGuid() },
            NavalLabMode.TwoClientNative), "A", null!, null!);
        AccessTools.PropertySetter(typeof(MissionBehavior), nameof(MissionBehavior.Mission)).Invoke(behavior, new object[] { scope.Instance });
        behavior.SendStationRelease = published.Add;
        behavior.Agents = Enumerable.Range(0, 10).Select(_ => Shell<Agent>()).ToArray();
        behavior.Ships = new[] { Shell<MissionShip>(), Shell<MissionShip>() };
        inventory = Keys.ToDictionary(key => key, _ =>
        {
            var machine = Shell<ShipOarMachine>();
            Set(machine, "<PilotStandingPoint>k__BackingField", Shell<StandingPoint>());
            return machine;
        });
    }

    private void Patch(MethodBase method, string prefix) => harmony.Patch(method, prefix: new HarmonyMethod(GetType(), prefix));
    private static bool True(ref bool __result) { __result = true; return false; }
    private static bool Zero(ref int __result) { __result = 0; return false; }
    private static bool NoController(ref AgentControllerType __result) { __result = AgentControllerType.None; return false; }
    private static bool Inventory(ref Dictionary<string, ShipOarMachine> __result) { __result = inventory; return false; }
    private static bool TargetWrite() { targetWrites++; return false; }
    private static bool StopUse(Agent __instance)
    {
        stops++;
        var machine = inventory.Values.First(candidate => candidate.PilotStandingPoint.UserAgent == __instance);
        Seat(machine, __instance, false);
        return false;
    }
    private static void Set(object target, string name, object? value) => AccessTools.Field(target.GetType(), name).SetValue(target, value);
    private static T Shell<T>()
    {
        var managed = typeof(ScriptComponentBehavior).BaseType!.Assembly.GetType("TaleWorlds.DotNet.Managed")!;
        var field = AccessTools.Field(managed, "_moduleTypes"); var previous = field.GetValue(null);
        try { if (previous == null) field.SetValue(null, new Dictionary<string, Type>()); return (T)FormatterServices.GetUninitializedObject(typeof(T)); }
        finally { field.SetValue(null, previous); }
    }

    // A complete native seat: point user, used object, sitting and last pilot, or a complete native stop-use.
    private static void Seat(ShipOarMachine machine, Agent agent, bool seated)
    {
        Set(machine.PilotStandingPoint, "_userAgent", seated ? agent : null);
        AccessTools.Field(typeof(Agent), "<CurrentlyUsedGameObject>k__BackingField").SetValue(agent, seated ? machine.PilotStandingPoint : null);
        Set(machine, "_isPilotSitting", seated);
        Set(machine, "_lastPilotAgent", agent);
    }

    private Agent Crew(int slot, int crew) => behavior.Agents[(slot * 5) + crew + 1];

    private NetworkNavalLabStations Commit(int slot)
    {
        var commit = new NetworkNavalLabStations(behavior.manifest.IncarnationId, 1, slot, "commit",
            behavior.manifest.Combatants.Skip((slot * 5) + 1).Take(4).ToArray(), Keys);
        behavior.appliedStations[slot] = commit;
        for (int crew = 0; crew < 4; crew++) Seat(inventory[Keys[crew]], Crew(slot, crew), true);
        Assert.True(behavior.ObserveStations(commit));
        return commit;
    }

    [Fact]
    public void OwnerNativeStopUse_PublishesOneOrderedRelease()
    {
        var commit = Commit(0);
        Seat(inventory[Keys[0]], Crew(0, 0), false);

        Assert.True(behavior.ObserveStations(commit));
        Assert.True(behavior.ObserveStations(commit));

        var release = Assert.Single(published);
        Assert.Equal(("release", 1L), (release.Phase, release.Revision));
        Assert.Equal(new[] { true, false, false, false }, release.Released);
    }

    [Theory]
    [InlineData("partial_stop")]
    [InlineData("never_seated")]
    public void OccupiedRowerPath_StillFaultsOnPartialOrInitialSeatLoss(string condition)
    {
        var commit = new NetworkNavalLabStations(behavior.manifest.IncarnationId, 1, 0, "commit",
            behavior.manifest.Combatants.Skip(1).Take(4).ToArray(), Keys);
        behavior.appliedStations[0] = commit;
        for (int crew = 1; crew < 4; crew++) Seat(inventory[Keys[crew]], Crew(0, crew), true);
        Seat(inventory[Keys[0]], Crew(0, 0), condition == "partial_stop");
        if (condition == "partial_stop")
        {
            Assert.True(behavior.ObserveStations(commit));
            Set(inventory[Keys[0]].PilotStandingPoint, "_userAgent", null);
        }

        Assert.False(behavior.ObserveStations(commit));
        Assert.Empty(published);
    }

    [Fact]
    public void ForeignBridgeSideStop_WaitsForOwnerRelease_ThenAppliesWithoutFault()
    {
        var commit = Commit(1);
        Seat(inventory[Keys[2]], Crew(1, 2), false);

        Assert.True(behavior.ObserveStations(commit));
        behavior.ApplyStations(commit.WithRelease(1, new[] { false, false, true, false }));
        behavior.unconfirmedReleaseDeadlines[1][2] = 1;

        Assert.True(behavior.ObserveStations(commit));
        Assert.Equal(0, stops);
        Assert.Empty(published);
    }

    [Fact]
    public void OwnerReleaseBeforeReplicaBridgeStop_DoesNotFaultOrStopTwice()
    {
        var commit = Commit(1);

        behavior.ApplyStations(commit.WithRelease(1, new[] { false, false, true, false }));
        behavior.unconfirmedReleaseDeadlines[1][2] = 1;

        Assert.True(behavior.ObserveStations(commit));
        Assert.Equal(1, stops);
        Assert.Empty(published);
    }

    [Fact]
    public void ForeignStopNeverConfirmedByOwner_Faults()
    {
        var commit = Commit(1);
        Seat(inventory[Keys[2]], Crew(1, 2), false);
        Assert.True(behavior.ObserveStations(commit));
        behavior.unconfirmedReleaseDeadlines[1][2] = 1;

        Assert.False(behavior.ObserveStations(commit));
        Assert.NotNull(behavior.firstStationObservationFailure);
    }

    [Fact]
    public void OwnerRelease_StopsThePuppetOnce_AndStaleOrLateStateNeverRepins()
    {
        var commit = Commit(1);

        behavior.ApplyStations(commit.WithRelease(1, new[] { true, true, true, true }));
        behavior.ApplyStations(commit.WithRelease(1, new[] { true, true, true, true }));
        behavior.ApplyStations(commit.WithRelease(0, new[] { false, false, false, false }));
        Seat(inventory[Keys[0]], Crew(1, 0), true);

        Assert.Equal(4, stops);
        Assert.True(behavior.ObserveStations(commit, refreshTargets: true));
        Assert.Equal(0, targetWrites);
    }

    [Theory]
    [InlineData("own_slot")]
    [InlineData("re_occupied")]
    [InlineData("skipped_revision")]
    public void InvalidReplayedRelease_Throws(string condition)
    {
        var commit = Commit(condition == "own_slot" ? 0 : 1);
        if (condition == "re_occupied") behavior.ApplyStations(commit.WithRelease(1, new[] { true, false, false, false }));
        var release = condition == "re_occupied" ? commit.WithRelease(2, new[] { false, true, false, false })
            : commit.WithRelease(condition == "skipped_revision" ? 2 : 1, new[] { true, false, false, false });

        Assert.Throws<InvalidOperationException>(() => behavior.ApplyStations(release));
    }

    public void Dispose()
    {
        harmony.UnpatchAll(harmony.Id);
        scope.Dispose();
    }
}
#endif
