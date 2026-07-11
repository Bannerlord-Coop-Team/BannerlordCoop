using Missions.Battles;
using Missions.Messages;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class SiegeMachineStateReplicatorTests
{
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
