using HarmonyLib;
using Missions.Battles;
using Missions.Messages;
using System;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

[Collection("Mission.Current")]
public class SiegeGateDestroyedReplayTests : IDisposable
{
    private const int GateId = 44;
    private const int DestroyedStateIndex = 2;
    private const float MaxHitPoints = 450f;

    private static readonly MethodInfo ApplySnapshotMethod =
        AccessTools.Method(typeof(SiegeMachineStateReplicator), "Apply");

    private static int destroyedReplays;
    private static Agent replayAgent;
    private static ScriptComponentBehavior replayAttacker;
    private static int replayDamage;
    private static int hitAnimations;

    private readonly Harmony harmony = new("coop.tests.siege-gate-destroyed-replay");

    public SiegeGateDestroyedReplayTests()
    {
        destroyedReplays = 0;
        replayAgent = null;
        replayAttacker = null;
        replayDamage = -1;
        hitAnimations = 0;
        Stub(AccessTools.Method(typeof(ScriptComponentBehavior), "CacheEditableFieldsForAllScriptComponents"), nameof(Skip));
        Stub(AccessTools.Method(typeof(CastleGate), "OnDestroyed"), nameof(RecordDestroyedReplay));
        Stub(AccessTools.Method(typeof(DestructableComponent), nameof(DestructableComponent.SetDestructionLevel)),
            nameof(RecordDestructionLevel));
        Stub(AccessTools.Method(typeof(SynchedMissionObject), nameof(SynchedMissionObject.SetAnimationAtChannelSynched),
            new[] { typeof(string), typeof(int), typeof(float) }), nameof(RecordHitAnimation));
        Stub(AccessTools.Method(typeof(DestructableComponent), nameof(DestructableComponent.BurstHeavyHitParticles)), nameof(Skip));
        Stub(AccessTools.Method(typeof(SoundEvent), nameof(SoundEvent.GetEventIdFromString)), nameof(NoSound));
        Stub(AccessTools.PropertyGetter(typeof(WeakGameEntity), nameof(WeakGameEntity.GlobalPosition)), nameof(Origin));
        Stub(AccessTools.Method(typeof(Mission), nameof(Mission.MakeSound),
            new[] { typeof(int), typeof(Vec3), typeof(bool), typeof(bool), typeof(int), typeof(int) }), nameof(Skip));
    }

    public void Dispose() => harmony.UnpatchAll(harmony.Id);

    [Fact]
    public void PeerSnapshot_FirstDestroyedTransition_ReplaysCastleGateOnDestroyedOnce()
    {
        var gate = CreateGate(MaxHitPoints);

        ApplySnapshot(gate, 0f, DestroyedStateIndex);

        Assert.Equal(DestroyedStateIndex, (int)AccessTools.Field(typeof(DestructableComponent), "_currentStateIndex")
            .GetValue(gate.DestructionComponent));
        Assert.Equal(1, destroyedReplays);
        Assert.Null(replayAgent);
        Assert.Null(replayAttacker);
        Assert.Equal(0, replayDamage);

        ApplySnapshot(gate, 0f, DestroyedStateIndex);
        Assert.Equal(1, destroyedReplays);
    }

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(MaxHitPoints, 120f)]
    public void PeerSnapshot_WithoutDestroyedTransition_DoesNotReplay(float localHitPoints, float snapshotHitPoints)
    {
        var gate = CreateGate(localHitPoints);

        ApplySnapshot(gate, snapshotHitPoints, snapshotHitPoints > 0f ? 0 : DestroyedStateIndex);

        Assert.Equal(0, destroyedReplays);
    }

    [Fact]
    public void PeerSnapshot_UpwardResendThenDestroyedAgain_ReplaysAgain()
    {
        var gate = CreateGate(0f);

        ApplySnapshot(gate, MaxHitPoints, 0);
        Assert.Equal(0, destroyedReplays);

        ApplySnapshot(gate, 0f, DestroyedStateIndex);
        Assert.Equal(1, destroyedReplays);
    }

    [Fact]
    public void PeerSnapshot_DestroyedWhileOpen_ReplaysOnce()
    {
        var gate = CreateGate(MaxHitPoints, CastleGate.GateState.Open);

        ApplySnapshot(gate, 0f, DestroyedStateIndex, (int)CastleGate.GateState.Open);

        Assert.Equal(1, destroyedReplays);
    }

    [Fact]
    public void CosmeticGateHit_OnDestroyedGate_DoesNotAnimateDoorOrPlank()
    {
        using var mission = new MissionCurrentScope();
        AddToMission(mission, CreateGate(0f, withDoorSkeleton: true));

        bool applied = new SiegeGateHitApplier().TryApply(CosmeticHit(), applyDamage: false);

        Assert.Equal(0, hitAnimations);
        Assert.True(applied);
    }

    [Fact]
    public void CosmeticGateHit_WithoutDoorSkeleton_DoesNotAnimate()
    {
        using var mission = new MissionCurrentScope();
        AddToMission(mission, CreateGate(MaxHitPoints, withDoorSkeleton: false));

        bool applied = new SiegeGateHitApplier().TryApply(CosmeticHit(), applyDamage: false);

        Assert.Equal(0, hitAnimations);
        Assert.True(applied);
    }

    [Fact]
    public void CosmeticGateHit_OnStandingGate_AnimatesDoorAndPlank()
    {
        using var mission = new MissionCurrentScope();
        AddToMission(mission, CreateGate(MaxHitPoints, withDoorSkeleton: true));

        bool applied = new SiegeGateHitApplier().TryApply(CosmeticHit(), applyDamage: false);

        Assert.Equal(2, hitAnimations);
        Assert.True(applied);
    }

    private static CastleGate CreateGate(
        float hitPoints,
        CastleGate.GateState state = CastleGate.GateState.Closed,
        bool withDoorSkeleton = true)
    {
#pragma warning disable SYSLIB0050
        var gate = (CastleGate)FormatterServices.GetUninitializedObject(typeof(CastleGate));
        var destruction = (DestructableComponent)FormatterServices.GetUninitializedObject(typeof(DestructableComponent));
        var door = (SynchedMissionObject)FormatterServices.GetUninitializedObject(typeof(SynchedMissionObject));
        var plank = (SynchedMissionObject)FormatterServices.GetUninitializedObject(typeof(SynchedMissionObject));
#pragma warning restore SYSLIB0050
        AccessTools.Property(typeof(MissionObject), "Id").SetValue(gate, new MissionObjectId(GateId, false));
        AccessTools.Field(typeof(UsableMachine), "<DestructionComponent>k__BackingField").SetValue(gate, destruction);
        AccessTools.Field(typeof(DestructableComponent), "_hitPoint").SetValue(destruction, hitPoints);
        destruction.MaxHitPoint = MaxHitPoints;
        AccessTools.Field(typeof(CastleGate), "<State>k__BackingField").SetValue(gate, state);
        AccessTools.Field(typeof(CastleGate), "_door").SetValue(gate, door);
        AccessTools.Field(typeof(CastleGate), "_plank").SetValue(gate, plank);
        AccessTools.Field(typeof(CastleGate), "_doorSkeleton").SetValue(gate, withDoorSkeleton ? CreateSkeleton() : null);
        return gate;
    }

    private static Skeleton CreateSkeleton()
    {
        // The native base type initializer reads the engine class table, which the bridge reports as empty.
        var managed = AccessTools.Field(AccessTools.TypeByName("TaleWorlds.DotNet.LibraryApplicationInterface"), "IManaged");
        object original = managed.GetValue(null);
        managed.SetValue(null, typeof(DispatchProxy).GetMethod(nameof(DispatchProxy.Create), Type.EmptyTypes)
            .MakeGenericMethod(managed.FieldType, typeof(EmptyEngineClassTable))
            .Invoke(null, null));
        try
        {
#pragma warning disable SYSLIB0050
            var skeleton = (Skeleton)FormatterServices.GetUninitializedObject(typeof(Skeleton));
#pragma warning restore SYSLIB0050
            GC.SuppressFinalize(skeleton);
            return skeleton;
        }
        finally
        {
            managed.SetValue(null, original);
        }
    }

    private static void ApplySnapshot(CastleGate gate, float hitPoints, int destructionState, int gateState = -1)
    {
        var state = new NetworkSiegeMachineState(GateId, hitPoints, destructionState, gateState, -1, -1f,
            false, -1, -1000f, -1000f, hostEpoch: 1, senderControllerId: "host");
        ApplySnapshotMethod.Invoke(null, new object[] { gate, state });
    }

    private static NetworkGateHit CosmeticHit() => new NetworkGateHit(GateId, 15, 350, "ram-owner", 1, 0);

    private static void AddToMission(MissionCurrentScope mission, CastleGate gate) =>
        ((MBList<MissionObject>)AccessTools.Field(typeof(Mission), "_missionObjects").GetValue(mission.Instance)).Add(gate);

    private void Stub(MethodBase method, string prefix) =>
        harmony.Patch(method, prefix: new HarmonyMethod(AccessTools.Method(typeof(SiegeGateDestroyedReplayTests), prefix)));

    private static bool Skip() => false;

    private static bool NoSound(ref int __result)
    {
        __result = 0;
        return false;
    }

    private static bool Origin(ref Vec3 __result)
    {
        __result = Vec3.Zero;
        return false;
    }

    private static bool RecordDestroyedReplay(Agent destroyerAgent, ScriptComponentBehavior attackerScriptComponentBehavior,
        int inflictedDamage)
    {
        destroyedReplays++;
        replayAgent = destroyerAgent;
        replayAttacker = attackerScriptComponentBehavior;
        replayDamage = inflictedDamage;
        return false;
    }

    private static bool RecordDestructionLevel(DestructableComponent __instance, int state)
    {
        AccessTools.Field(typeof(DestructableComponent), "_currentStateIndex").SetValue(__instance, state);
        return false;
    }

    private static bool RecordHitAnimation()
    {
        hitAnimations++;
        return false;
    }

    public class EmptyEngineClassTable : DispatchProxy
    {
        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            if (targetMethod.Name == "GetClassTypeDefinitionCount") return 0;
            throw new InvalidOperationException("Unexpected native call: " + targetMethod.Name);
        }
    }
}
