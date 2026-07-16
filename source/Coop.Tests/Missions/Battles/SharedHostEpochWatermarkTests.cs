using Common;
using Common.Tests.Utils;
using GameInterface.Services.MapEvents;
using Missions;
using Missions.Battles;
using Missions.Messages;
using Moq;
using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>
/// BR-102: the accepted-epoch high-water mark is shared by a battle's TWO siege replicators, because
/// <see cref="CoopBattleController"/> injects ONE <see cref="IHostEpochPolicy"/> and passes that same
/// instance to both. A superseded hosting generation must therefore be dropped consistently across
/// every host-authority message type: once ANY of them accepts epoch 7, a delayed epoch-6 message of a
/// DIFFERENT type is stale too. These tests drive the real subscribe/handle pipeline of both replicators
/// against a single identity-only <c>Mission.Current</c>, differing only in whether the two replicators
/// share one policy or hold separate ones.
/// </summary>
[Collection("Mission.Current")]
public class SharedHostEpochWatermarkTests : IDisposable
{
    private const int LocalEpoch = 5;

    private readonly TestMessageBroker broker = new();
    private readonly Mock<IBattleNetwork> network = new();
    private readonly Mock<IBattleSession> session = new();
    private readonly Mock<INetworkAgentRegistry> agentRegistry = new();
    private readonly MissionCurrentScope missionScope = new();

    private SiegeEngineDeploymentReplicator engineReplicator;
    private SiegeMachineStateReplicator machineReplicator;

    public SharedHostEpochWatermarkTests()
    {
        session.SetupGet(s => s.InstanceId).Returns("mapEvent1");
        session.SetupGet(s => s.OwnControllerId).Returns("us");
        session.SetupGet(s => s.IsLocalHost).Returns(false);
        session.SetupGet(s => s.HostEpoch).Returns(LocalEpoch);
    }

    [Fact]
    [Trait("Requirement", "BR-102")]
    public void OneSharedPolicy_DropsADelayedLowerEpochOfADifferentMessageType()
    {
        // Both replicators share ONE policy, exactly as CoopBattleController wires them.
        var sharedPolicy = new HostEpochPolicy();
        Compose(sharedPolicy, sharedPolicy);

        // A machine-state snapshot from a newer generation (epoch 7) is accepted and buffered — and it
        // raises the SHARED watermark to 7.
        broker.Publish(this, MachineState(machineId: 24, hostEpoch: LocalEpoch + 2));
        DrainGameThread();
        Assert.True(PendingStates().ContainsKey(24));

        // A delayed engine placement from the SUPERSEDED epoch-6 generation is still ahead of the stored
        // assignment (6 > 5), so its own per-message assignment check would accept it — but the shared
        // watermark (7, raised by the machine snapshot above) makes the engine replicator drop it before
        // the authoritative history. This is the cross-message-type ordering guarantee sharing buys.
        broker.Publish(this, new NetworkSiegeEnginePlacement(1, "Ballista", hostEpoch: LocalEpoch + 1));
        DrainGameThread();

        Assert.Empty(Placements());
    }

    [Fact]
    [Trait("Requirement", "BR-102")]
    public void SeparatePolicies_DoNotShareTheWatermark_SoTheDelayedPlacementIsAccepted()
    {
        // The contrast case: give each replicator its OWN policy. The machine snapshot raises only the
        // machine replicator's watermark; the engine replicator's watermark stays 0, so the same delayed
        // epoch-6 placement (6 > 5) is accepted and recorded. This is precisely the corruption that
        // sharing one policy prevents — proving the drop above is the SHARED watermark, not the
        // assignment check.
        Compose(new HostEpochPolicy(), new HostEpochPolicy());

        broker.Publish(this, MachineState(machineId: 24, hostEpoch: LocalEpoch + 2));
        DrainGameThread();

        broker.Publish(this, new NetworkSiegeEnginePlacement(1, "Ballista", hostEpoch: LocalEpoch + 1));
        DrainGameThread();

        var placement = Assert.Single(Placements());
        Assert.Equal(1, placement.Key);
        Assert.Equal("Ballista", placement.Value);
    }

    // ------------------------------------------------------------------
    // Plumbing
    // ------------------------------------------------------------------

    private void Compose(IHostEpochPolicy enginePolicy, IHostEpochPolicy machinePolicy)
    {
        engineReplicator = new SiegeEngineDeploymentReplicator(network.Object, broker, session.Object, enginePolicy);
        machineReplicator = new SiegeMachineStateReplicator(network.Object, broker, session.Object, agentRegistry.Object, machinePolicy);
    }

    private static NetworkSiegeMachineState MachineState(int machineId, int hostEpoch)
        => new(machineId, hitPoints: -1f, destructionState: -1, gateState: -1, ladderState: -1,
            moveDistance: -1f, hasArrived: false, weaponState: -1, aimDirection: -1000f,
            aimReleaseAngle: -1000f, hostEpoch: hostEpoch);

    private List<KeyValuePair<int, string>> Placements()
    {
        var field = typeof(SiegeEngineDeploymentReplicator).GetField(
            "placements", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<List<KeyValuePair<int, string>>>(field!.GetValue(engineReplicator));
    }

    private Dictionary<int, NetworkSiegeMachineState> PendingStates()
    {
        var field = typeof(SiegeMachineStateReplicator).GetField(
            "pendingByMachineId", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<Dictionary<int, NetworkSiegeMachineState>>(field!.GetValue(machineReplicator));
    }

    /// <summary>Handlers queue their bodies via <c>GameThread.RunSafe</c>; a blocking no-op queued
    /// after them (FIFO) proves they have run on the test game-loop pump before assertions read.</summary>
    private static void DrainGameThread() => GameThread.Run(() => { }, blocking: true);

    public void Dispose()
    {
        engineReplicator?.Dispose();
        machineReplicator?.Dispose();
        missionScope.Dispose();
        SiegeMissionAuthorityGate.ResetClaimedMachines();
    }
}
