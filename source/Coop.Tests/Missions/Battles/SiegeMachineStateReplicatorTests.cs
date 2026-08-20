using GameInterface.Surrogates;
using Missions.Battles;
using Missions.Messages;
using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class SiegeMachineStateReplicatorTests
{
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
            stoneAmmo: 7);

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
            hostEpoch: 4);

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
