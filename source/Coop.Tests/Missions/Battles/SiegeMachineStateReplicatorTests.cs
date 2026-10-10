using GameInterface.Surrogates;
using Missions.Battles;
using HarmonyLib;
using Missions.Messages;
using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

[Collection("Mission.Current")]
public class SiegeMachineStateReplicatorTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PeerMangonel_LoadingPointFollowsReceivedStateWithoutRunningWeaponState(bool catchUp)
    {
        var harmony = new Harmony("coop.tests.peer-mangonel." + Guid.NewGuid());
        try
        {
            harmony.Patch(AccessTools.Method(typeof(ScriptComponentBehavior), "CacheEditableFieldsForAllScriptComponents"),
                prefix: new HarmonyMethod(typeof(SiegeMachineStateReplicatorTests), nameof(SkipNativeCache)));
            harmony.Patch(AccessTools.Method(typeof(UsableMissionObject), nameof(UsableMissionObject.SetIsDeactivatedSynched)),
                prefix: new HarmonyMethod(typeof(SiegeMachineStateReplicatorTests), nameof(RecordPointActivation)));
#pragma warning disable SYSLIB0050
            var weapon = (Mangonel)FormatterServices.GetUninitializedObject(typeof(Mangonel));
            var point = (StandingPointWithWeaponRequirement)FormatterServices.GetUninitializedObject(typeof(StandingPointWithWeaponRequirement));
            var ballista = (Ballista)FormatterServices.GetUninitializedObject(typeof(Ballista));
            var sut = (SiegeMachineStateReplicator)FormatterServices.GetUninitializedObject(typeof(SiegeMachineStateReplicator));
#pragma warning restore SYSLIB0050
            AccessTools.Property(typeof(MissionObject), "Id").SetValue(weapon, new MissionObjectId(1554, false));
            AccessTools.Property(typeof(MissionObject), "Id").SetValue(ballista, new MissionObjectId(884, false));
            AccessTools.Field(typeof(RangedSiegeWeapon), "LoadAmmoStandingPoint").SetValue(weapon, point);
            AccessTools.Field(typeof(UsableMissionObject), "_isDeactivated").SetValue(point, true);
            AccessTools.Field(typeof(SiegeMachineStateReplicator), "peerWeaponState").SetValue(sut, new Dictionary<int, int>());
            pointActivationCalls = 0;
            var apply = AccessTools.Method(typeof(SiegeMachineStateReplicator), "ApplyWeaponAnimation");
            if (!catchUp) apply.Invoke(sut, new object[] { weapon, (int)RangedSiegeWeapon.WeaponState.Reloading });
            int before = pointActivationCalls;
            apply.Invoke(sut, new object[] { weapon, (int)RangedSiegeWeapon.WeaponState.LoadingAmmo });
            Assert.False(point.IsDeactivated);
            Assert.Equal(before + 1, pointActivationCalls);
            Assert.Equal(RangedSiegeWeapon.WeaponState.Idle, weapon.State);
            apply.Invoke(sut, new object[] { weapon, (int)RangedSiegeWeapon.WeaponState.LoadingAmmo });
            Assert.Equal(before + 1, pointActivationCalls);
            // Local simulation may change the point while the peer animation cache is retained.
            AccessTools.Field(typeof(UsableMissionObject), "_isDeactivated").SetValue(point, true);
            apply.Invoke(sut, new object[] { weapon, (int)RangedSiegeWeapon.WeaponState.LoadingAmmo });
            Assert.False(point.IsDeactivated);
            Assert.Equal(before + 2, pointActivationCalls);
            apply.Invoke(sut, new object[] { weapon, -1 });
            Assert.False(point.IsDeactivated);
            Assert.Equal(before + 2, pointActivationCalls);
            apply.Invoke(sut, new object[] { ballista, (int)RangedSiegeWeapon.WeaponState.LoadingAmmo });
            Assert.Equal(before + 2, pointActivationCalls);
            apply.Invoke(sut, new object[] { weapon, (int)RangedSiegeWeapon.WeaponState.WaitingBeforeIdle });
            Assert.True(point.IsDeactivated);
            Assert.Equal(before + 3, pointActivationCalls);
            Assert.Equal(RangedSiegeWeapon.WeaponState.Idle, weapon.State);
        }
        finally { harmony.UnpatchAll(harmony.Id); }
    }

    private static int pointActivationCalls;

    private static bool SkipNativeCache() => false;

    private static bool RecordPointActivation(UsableMissionObject __instance, bool value)
    {
        pointActivationCalls++;
        AccessTools.Field(typeof(UsableMissionObject), "_isDeactivated").SetValue(__instance, value);
        return false;
    }

    [Fact]
    public void NetworkGateHit_RoundTripsRamAuthorityAndDamage()
    {
        var original = new NetworkGateHit(12, 15, 350, "ram-owner", 8, 4);
        var result = ProtoBuf.Serializer.DeepClone(original);
        Assert.Equal(original.GateId, result.GateId);
        Assert.Equal(original.RamId, result.RamId);
        Assert.Equal(original.Damage, result.Damage);
        Assert.Equal(original.SenderControllerId, result.SenderControllerId);
        Assert.Equal(original.HostEpoch, result.HostEpoch);
        Assert.Equal(original.AuthorityRevision, result.AuthorityRevision);
    }

    [Fact]
    public void NetworkSiegeWeaponFired_RoundTripsMachineAuthorityAndProjectile()
    {
        _ = new SurrogateCollection();
        var original = new NetworkSiegeWeaponFired(12, Guid.NewGuid(), new Vec3(1f, 2f, 3f),
            new Vec3(0f, 1f, 0f), Mat3.Identity, 30f, 35f, "stone", "owner-b", 8, 4);
        var result = ProtoBuf.Serializer.DeepClone(original);
        Assert.Equal(original.MachineId, result.MachineId);
        Assert.Equal(original.ShooterAgentId, result.ShooterAgentId);
        Assert.Equal(original.Position, result.Position);
        Assert.Equal(original.Direction, result.Direction);
        Assert.Equal(original.BaseSpeed, result.BaseSpeed);
        Assert.Equal(original.Speed, result.Speed);
        Assert.Equal(original.MissileItemId, result.MissileItemId);
        Assert.Equal(original.SenderControllerId, result.SenderControllerId);
        Assert.Equal(original.HostEpoch, result.HostEpoch);
        Assert.Equal(original.AuthorityRevision, result.AuthorityRevision);
    }

#if DEBUG
    [Fact]
    public void NativeInputRequest_RoundTripsTargetAndOneShotIdentity()
    {
        var original = new NetworkSiegeInteractionDebugRequest(
            "map-event-1", "testclient2", "gate-use-1", 12, "use", 2);
        var result = ProtoBuf.Serializer.DeepClone(original);
        Assert.Equal(original.MapEventId, result.MapEventId);
        Assert.Equal(original.ControllerId, result.ControllerId);
        Assert.Equal(original.RequestId, result.RequestId);
        Assert.Equal(original.MachineId, result.MachineId);
        Assert.Equal(original.Action, result.Action);
        Assert.Equal(original.StandingPointIndex, result.StandingPointIndex);
    }
#endif

    [Fact]
    public void NetworkSiegeMachineAuthority_RoundTripsOrderingIdentity()
    {
        var original = new NetworkSiegeMachineAuthority(
            machineId: 12,
            controllerId: "owner-a",
            hostEpoch: 4,
            authorityRevision: 3,
            senderControllerId: "host-a");

        NetworkSiegeMachineAuthority result;
        using (var stream = new MemoryStream())
        {
            RuntimeTypeModel.Default.Serialize(stream, original);
            stream.Position = 0;
            result = (NetworkSiegeMachineAuthority)RuntimeTypeModel.Default.Deserialize(
                stream,
                null,
                typeof(NetworkSiegeMachineAuthority));
        }

        Assert.Equal(original.MachineId, result.MachineId);
        Assert.Equal(original.ControllerId, result.ControllerId);
        Assert.Equal(original.HostEpoch, result.HostEpoch);
        Assert.Equal(original.AuthorityRevision, result.AuthorityRevision);
        Assert.Equal(original.SenderControllerId, result.SenderControllerId);
    }

    [Fact]
    public void NetworkSiegeMachineState_RoundTripsSimulatorOwnedState()
    {
        var original = new NetworkSiegeMachineState(
            machineId: 12,
            hitPoints: -1f,
            destructionState: -1,
            gateState: -1,
            ladderState: (int)SiegeLadder.LadderState.BeingRaised,
            moveDistance: -1f,
            hasArrived: false,
            weaponState: -1,
            aimDirection: -1000f,
            aimReleaseAngle: -1000f,
            hostEpoch: 4,
            stoneAmmo: 7,
            senderControllerId: "peer-a",
            authorityRevision: 3);

        NetworkSiegeMachineState result;
        using (var stream = new MemoryStream())
        {
            RuntimeTypeModel.Default.Serialize(stream, original);
            stream.Position = 0;
            result = (NetworkSiegeMachineState)RuntimeTypeModel.Default.Deserialize(
                stream,
                null,
                typeof(NetworkSiegeMachineState));
        }

        Assert.Equal(original.LadderState, result.LadderState);
        Assert.True(result.HasStoneAmmo);
        Assert.Equal(original.StoneAmmo, result.StoneAmmo);
        Assert.Equal(original.HostEpoch, result.HostEpoch);
        Assert.Equal(original.SenderControllerId, result.SenderControllerId);
        Assert.Equal(original.AuthorityRevision, result.AuthorityRevision);
    }

    [Fact]
    public void NetworkSiegeLadderAnimationState_RoundTripsSnapshot()
    {
        _ = new SurrogateCollection();
        var ladderFrame = new MatrixFrame(Mat3.Identity, new Vec3(1f, 2f, 3f));
        var original = new NetworkSiegeLadderAnimationState(
            ladderId: 12,
            animationSpeed: 1.73f,
            animationProgress: 0.42f,
            animationState: (int)SiegeLadder.LadderAnimationState.PhysicallyDynamic,
            fallAngularSpeed: -0.5f,
            frame: ladderFrame,
            animationIndex: 17,
            hostEpoch: 4,
            senderControllerId: "peer-a",
            authorityRevision: 3);

        NetworkSiegeLadderAnimationState result;
        using (var stream = new MemoryStream())
        {
            RuntimeTypeModel.Default.Serialize(stream, original);
            stream.Position = 0;
            result = (NetworkSiegeLadderAnimationState)RuntimeTypeModel.Default.Deserialize(
                stream,
                null,
                typeof(NetworkSiegeLadderAnimationState));
        }

        Assert.Equal(original.LadderId, result.LadderId);
        Assert.Equal(original.AnimationSpeed, result.AnimationSpeed);
        Assert.Equal(original.AnimationProgress, result.AnimationProgress);
        Assert.Equal(original.AnimationState, result.AnimationState);
        Assert.Equal(original.FallAngularSpeed, result.FallAngularSpeed);
        Assert.Equal(original.Frame.origin, result.Frame.origin);
        Assert.Equal(original.AnimationIndex, result.AnimationIndex);
        Assert.Equal(original.HostEpoch, result.HostEpoch);
        Assert.Equal(original.SenderControllerId, result.SenderControllerId);
        Assert.Equal(original.AuthorityRevision, result.AuthorityRevision);
    }

    [Fact]
    public void AuthoritativeHitPoints_UpdateTheMappedMissionSiegeWeapon()
    {
        var destruction = new object();
        var backingWeapon = MissionSiegeWeapon.CreateCampaignWeapon(null, 3, 100f, 100f);
        var deployed = new Dictionary<object, MissionSiegeWeapon>
        {
            [destruction] = backingWeapon,
        };

        bool updated = InvokeGenericStatic<bool, object>(
            "TrySyncBackingWeaponHealth",
            deployed,
            destruction,
            37.5f);

        Assert.True(updated);
        Assert.Equal(37.5f, backingWeapon.Health);
    }

    [Fact]
    public void ClaimantJoinCatchUp_SendsFreshStableStateFromTheActualSimulator()
    {
        const int claimedMachineId = 42;
        var claims = new Dictionary<int, string>
        {
            [claimedMachineId] = "claimant",
            [99] = "another-peer",
        };
        var stableState = new NetworkSiegeMachineState(
            claimedMachineId,
            hitPoints: -1f,
            destructionState: -1,
            gateState: 2,
            ladderState: -1,
            moveDistance: 18f,
            hasArrived: true,
            weaponState: 4,
            aimDirection: 0.75f,
            aimReleaseAngle: 0.25f);
        var captures = new List<(int MachineId, bool SimulatedLocally)>();
        var sent = new List<NetworkSiegeMachineState>();

        int count = InvokeStatic<int>(
            "SendJoinStateSnapshots",
            false,
            "claimant",
            new[] { claimedMachineId, 99, 100 },
            claims,
            new Func<int, bool, NetworkSiegeMachineState>((machineId, simulatedLocally) =>
            {
                captures.Add((machineId, simulatedLocally));
                return stableState;
            }),
            new Action<NetworkSiegeMachineState>(sent.Add));

        Assert.Equal(1, count);
        Assert.Equal(new[] { (claimedMachineId, true) }, captures);
        Assert.Same(stableState, Assert.Single(sent));
        Assert.True(sent[0].HasArrived);
        Assert.Equal(18f, sent[0].MoveDistance);
    }

    private static T InvokeStatic<T>(string methodName, params object[] arguments)
    {
        var method = typeof(SiegeMachineStateReplicator).GetMethod(
            methodName, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<T>(method.Invoke(null, arguments));
    }

    private static T InvokeGenericStatic<T, TKey>(string methodName, params object[] arguments)
    {
        var method = typeof(SiegeMachineStateReplicator).GetMethod(
            methodName, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<T>(method.MakeGenericMethod(typeof(TKey)).Invoke(null, arguments));
    }
}
