#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using HarmonyLib;
using Missions.Battles;
using Missions.Naval;
using NavalDLC.Missions.NavalPhysics;
using NavalDLC.Missions.Objects;
using NavalDLC.Missions.ShipActuators;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using Xunit;

namespace Coop.Tests.Missions.Battles;

// Covers the pulse physics window, its cross-thread attribution and the real rowing postfix binding.
[Collection("Mission.Current")]
public sealed class NavalLabPulsePhysicsObservationTests : IDisposable
{
    private readonly Harmony harmony = new("coop.tests.naval.pulse-physics");
    private readonly NavalLabBehavior.PulsePhysicsObservation observation = new();
    private readonly MissionShip own = Shell<MissionShip>();
    private readonly MissionShip foreign = Shell<MissionShip>();

    public void Dispose()
    {
        harmony.UnpatchAll(harmony.Id);
        NavalLabPhysicsPatches.Active = null;
    }

    // Builds an engine-free managed shell for native-backed script components.
    private static T Shell<T>()
    {
        var managed = typeof(ScriptComponentBehavior).BaseType!.Assembly.GetType("TaleWorlds.DotNet.Managed")!;
        var field = AccessTools.Field(managed, "_moduleTypes"); var previous = field.GetValue(null);
        try { if (previous == null) field.SetValue(null, new Dictionary<string, Type>()); return (T)FormatterServices.GetUninitializedObject(typeof(T)); }
        finally { field.SetValue(null, previous); }
    }

    private static void Set(object target, string name, object? value) => AccessTools.Field(target.GetType(), name).SetValue(target, value);
    private static bool Zero(ref int __result) { __result = 0; return false; }
    private JObject Read() => JObject.FromObject(observation.Snapshot());

    private void Sample(MissionShip ship, int generation, float force = 2, float speed = 0.5f, bool anchored = false) =>
        observation.Record(ship, generation, 0.5f, 1, 3, 4, 1.5f, 2.5f, force, speed, anchored, 0.8f);

    [Fact]
    public void NoOperationOrNoSampleIsExplicitlyUnavailableNotZeros()
    {
        Assert.Equal("no_operation", (string)Read()["unavailable"]!);
        var operation = Guid.NewGuid();
        observation.Begin(operation, own);
        var empty = Read();
        Assert.Equal("no_own_hull_sample", (string)empty["unavailable"]!);
        Assert.Equal(operation, (Guid)empty["operationId"]!);
        Assert.Equal(0, (int)empty["samples"]!);
        Assert.Null(empty["oarForwardImpulse"]);
    }

    [Fact]
    public void OwnHullSamplesAggregateAndNextOperationResetsAccumulator()
    {
        var first = Guid.NewGuid();
        observation.Begin(first, own);
        int generation = observation.Generation(own);
        Sample(own, generation, force: 2, speed: 0.5f);
        Sample(own, generation, force: 4, speed: 1, anchored: true);
        var summary = Read();
        Assert.Equal(first, (Guid)summary["operationId"]!); Assert.True((bool)summary["open"]!);
        Assert.Equal(2, (int)summary["samples"]!); Assert.Equal(1f, (float)summary["sampledSeconds"]!);
        Assert.Equal(2, (int)summary["thrustSamples"]!); Assert.Equal(1f, (float)summary["rowerThrustMax"]!);
        Assert.Equal(3, (int)summary["usedOarsMin"]!); Assert.Equal(4, (int)summary["totalOars"]!);
        Assert.Equal(4f, (float)summary["oarForwardForceMax"]!); Assert.Equal(3f, (float)summary["oarForwardImpulse"]!);
        Assert.Equal(0.5f, (float)summary["bodyForwardSpeedFirst"]!); Assert.Equal(1f, (float)summary["bodyForwardSpeedLast"]!);
        Assert.Equal(1, (int)summary["anchoredSamples"]!); Assert.Equal(0.8f, (float)summary["submergedFactorMin"]!);
        Assert.Equal(1.5f, (float)summary["phaseLast"]!); Assert.Equal(2.5f, (float)summary["phaseRateMax"]!);

        var second = Guid.NewGuid();
        observation.Begin(second, own);
        var reset = Read();
        Assert.Equal(second, (Guid)reset["operationId"]!);
        Assert.Equal(0, (int)reset["samples"]!);
    }

    [Fact]
    public void SampleStartedBeforeNextOperationIsNotAttributedToIt()
    {
        observation.Begin(Guid.NewGuid(), own);
        int old = observation.Generation(own);
        observation.Begin(Guid.NewGuid(), own);
        Sample(own, old);
        Assert.Equal(0, (int)Read()["samples"]!);
        Sample(own, observation.Generation(own));
        Assert.Equal(1, (int)Read()["samples"]!);
    }

    [Fact]
    public void CloseRetainsFinalSummaryAndIgnoresLaterSamples()
    {
        observation.Begin(Guid.NewGuid(), own);
        int generation = observation.Generation(own);
        Sample(own, generation, force: 7);
        observation.Close();
        string final = Read().ToString();
        Assert.Equal(-1, observation.Generation(own));
        Sample(own, generation, force: 100);
        Assert.Equal(final, Read().ToString());
        Assert.False((bool)Read()["open"]!);
        Assert.Equal(7f, (float)Read()["oarForwardForceMax"]!);
    }

    [Fact]
    public void ForeignHullIsNeverSampled()
    {
        observation.Begin(Guid.NewGuid(), own);
        Assert.Equal(-1, observation.Generation(foreign));
        Sample(foreign, observation.Generation(own));
        Assert.Equal(0, (int)Read()["samples"]!);
    }

    [Fact]
    public void NonfiniteSampleExportsNullNotNaN()
    {
        observation.Begin(Guid.NewGuid(), own);
        Sample(own, observation.Generation(own), speed: float.NaN);
        string json = JsonConvert.SerializeObject(observation.Snapshot());
        Assert.DoesNotContain("NaN", json); Assert.DoesNotContain("Infinity", json);
        Assert.Equal(JTokenType.Null, JObject.Parse(json)["bodyForwardSpeedLast"]!.Type);
    }

    // Every snapshot must be one consistent cut of a concurrently written window.
    [Fact]
    public void SnapshotsDuringConcurrentSamplingAreCoherent()
    {
        observation.Begin(Guid.NewGuid(), own);
        int generation = observation.Generation(own);
        var writer = Task.Run(() =>
        {
            for (int i = 0; i < 200000; i++) observation.Record(own, generation, 0.5f, 1, 3, 4, 0, 0, 2, 0, true, 1);
        });
        int checkedSnapshots = 0;
        while (!writer.IsCompleted || checkedSnapshots == 0)
        {
            var summary = Read();
            int samples = (int)summary["samples"]!;
            if (samples == 0) continue;
            Assert.Equal(samples * 0.5f, (float)summary["sampledSeconds"]!);
            Assert.Equal(samples * 1f, (float)summary["oarForwardImpulse"]!);
            Assert.Equal(samples, (int)summary["anchoredSamples"]!);
            Assert.Equal(samples, (int)summary["thrustSamples"]!);
            checkedSnapshots++;
        }
        writer.Wait();
        Assert.Equal(200000, (int)Read()["samples"]!);
    }

    [Fact]
    public void RowingPatchBindsActualSignatureAndSamplesOnlyOwnHull()
    {
        // ShipActuators static sound ids are native lookups.
        harmony.Patch(AccessTools.Method(typeof(SoundEvent), nameof(SoundEvent.GetEventIdFromString)),
            prefix: new HarmonyMethod(GetType(), nameof(Zero)));
        harmony.Patch(AccessTools.Method(typeof(SoundManager), nameof(SoundManager.GetEventGlobalIndex)),
            prefix: new HarmonyMethod(GetType(), nameof(Zero)));
        var target = AccessTools.Method(typeof(ShipActuators), "FixedUpdateRowers");
        Assert.Equal(new[] { "fixedDt", "actuatorInput", "shipEntityGlobalFrame", "shipForwardSpeed" }, target.GetParameters().Select(p => p.Name));
        Assert.Equal(new[] { typeof(float), typeof(ShipActuatorRecord).MakeByRefType(), typeof(MatrixFrame).MakeByRefType(), typeof(float) },
            target.GetParameters().Select(p => p.ParameterType));
        var patch = typeof(NavalLabPhysicsPatches).GetNestedType("PulseRowingObservation", BindingFlags.NonPublic)!;
        Assert.Single(harmony.CreateClassProcessor(patch).Patch());
        var info = Harmony.GetPatchInfo(target);
        Assert.Equal(harmony.Id, Assert.Single(info.Prefixes).owner); Assert.Equal(harmony.Id, Assert.Single(info.Postfixes).owner);
        Assert.Empty(info.Transpilers); Assert.Empty(info.Finalizers);

        var id = Guid.NewGuid();
        var behavior = new NavalLabBehavior(new NavalLabManifest("naval-lab:" + id.ToString("N"), id, new[] { "A", "B" },
            Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToArray(), new[] { Guid.NewGuid(), Guid.NewGuid() }, NavalLabMode.TwoClientNative), "B", null!, null!);
        NavalLabPhysicsPatches.Active = behavior;
        behavior.pulsePhysicsObservation.Begin(id, own);
        var actuators = Actuators(own, anchored: true);
        var foreignActuators = Actuators(foreign, anchored: false);
        // Body forward has a vertical part; vanilla and the observer both project on the flattened axis.
        var frame = new MatrixFrame(new Mat3(new Vec3(1, 0, 0), new Vec3(0, 1, 1), new Vec3(0, 0, 1)), Vec3.Zero);
        var record = new ShipActuatorRecord(1, 0, 0, 0, 0, 0);
        foreach (var hull in new[] { actuators, foreignActuators })
        {
            var prefixArgs = new object?[] { hull, null };
            patch.GetMethod("Prefix", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, prefixArgs);
            patch.GetMethod("Postfix", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null,
                new object?[] { hull, prefixArgs[1], 0.02f, record, frame, 0.7f });
        }
        var summary = JObject.FromObject(behavior.pulsePhysicsObservation.Snapshot());
        Assert.Equal(1, (int)summary["samples"]!);
        Assert.Equal(1f, (float)summary["rowerThrustMax"]!);
        Assert.Equal(2, (int)summary["usedOarsMin"]!); Assert.Equal(3, (int)summary["totalOars"]!);
        Assert.Equal(5f, (float)summary["oarForwardForceMax"]!);
        Assert.Equal(0.7f, (float)summary["bodyForwardSpeedFirst"]!);
        Assert.Equal(1, (int)summary["anchoredSamples"]!); Assert.Equal(0.9f, (float)summary["submergedFactorLast"]!);
        Assert.Equal(1.25f, (float)summary["phaseLast"]!); Assert.Equal(3.5f, (float)summary["phaseRateLast"]!);
    }

    // Managed-only actuator shell: two left oars with one used, one used right oar, and fixed oar forces.
    private static ShipActuators Actuators(MissionShip ship, bool anchored)
    {
        var physics = Shell<NavalPhysics>();
        Set(physics, "<IsAnchored>k__BackingField", anchored);
        Set(physics, "<LastSubmergedHeightFactorForActuators>k__BackingField", 0.9f);
        Set(ship, "_physics", physics);
        var actuators = (ShipActuators)FormatterServices.GetUninitializedObject(typeof(ShipActuators));
        Set(actuators, "_ownerMissionShip", ship);
        Set(actuators, "_leftSideOars", new MBList<(GameEntity, MissionOar)> { (null!, Oar(true)), (null!, Oar(false)) });
        Set(actuators, "_rightSideOars", new MBList<(GameEntity, MissionOar)> { (null!, Oar(true)) });
        Set(actuators, "_leftOarForces", new MBList<ShipForce> { new ShipForce(Vec3.Zero, new Vec3(0, 3, 0), ShipForce.SourceType.Oar, 1) });
        Set(actuators, "_rightOarForces", new MBList<ShipForce>
        {
            new ShipForce(Vec3.Zero, new Vec3(0, 2, 0), ShipForce.SourceType.Oar, 1),
            new ShipForce(Vec3.Zero, new Vec3(1, 0, 0), ShipForce.SourceType.Oar, 1)
        });
        Set(actuators, "_rowersPhase", 1.25f);
        Set(actuators, "_lastFramePhaseRate", 3.5f);
        return actuators;
    }

    private static MissionOar Oar(bool used)
    {
        var oar = (MissionOar)FormatterServices.GetUninitializedObject(typeof(MissionOar));
        Set(oar, "<IsUsed>k__BackingField", used);
        return oar;
    }
}
#endif
