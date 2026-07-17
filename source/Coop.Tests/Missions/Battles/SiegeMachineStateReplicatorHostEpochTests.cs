using Common;
using Common.Messaging;
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
/// BR-102 for the machine-simulation mesh traffic of <see cref="SiegeMachineStateReplicator"/>:
/// <see cref="NetworkSiegeMachineAuthority"/> (the host's arbitration of who simulates a machine) and
/// <see cref="NetworkSiegeMachineState"/> (per-machine snapshots whose host-owned fields carry damage)
/// are stamped with the sender's host epoch, and receivers drop a message stamped by an earlier
/// hosting generation — a deposed host still arbitrating or broadcasting in flight across a migration.
/// Unstamped (epoch 0) and ahead-of-receiver epochs are accepted per <see cref="HostEpochPolicy"/>.
/// <para>
/// Drives the REAL subscribe/handle pipeline: an identity-only <c>Mission.Current</c>
/// (<see cref="MissionCurrentScope"/>) lets the handlers run their bodies; authority decisions are
/// observed in the replicator's claim table, and machine states for a not-yet-registered machine in
/// its pending buffer (the applied-vs-dropped seam that needs no native machine objects).
/// </para>
/// </summary>
[Collection("Mission.Current")]
public class SiegeMachineStateReplicatorHostEpochTests : IDisposable
{
    private const int LocalEpoch = 5;

    private readonly TestMessageBroker broker = new();
    private readonly Mock<IBattleNetwork> network = new();
    private readonly Mock<IBattleSession> session = new();
    private readonly Mock<INetworkAgentRegistry> agentRegistry = new();
    private readonly List<IMessage> sentToAll = new();
    private readonly MissionCurrentScope missionScope = new();
    private readonly SiegeMachineStateReplicator sut;

    public SiegeMachineStateReplicatorHostEpochTests()
    {
        session.SetupGet(s => s.InstanceId).Returns("mapEvent1");
        session.SetupGet(s => s.OwnControllerId).Returns("us");
        session.SetupGet(s => s.IsLocalHost).Returns(false);
        session.SetupGet(s => s.HostEpoch).Returns(LocalEpoch);
        network.Setup(n => n.SendAll(It.IsAny<IMessage>())).Callback<IMessage>(sentToAll.Add);

        sut = new SiegeMachineStateReplicator(network.Object, broker, session.Object, agentRegistry.Object, new HostEpochPolicy());
    }

    // ------------------------------------------------------------------
    // NetworkSiegeMachineAuthority — receiver gate
    // ------------------------------------------------------------------

    [Fact]
    [Trait("Requirement", "BR-102")]
    public void StaleEpochAuthority_IsDropped_AndCurrentEpochAuthorityApplies()
    {
        // A deposed host's in-flight arbitration (stamped by the previous hosting generation) must not
        // move a machine's simulation.
        broker.Publish(this, new NetworkSiegeMachineAuthority(7, "us", hostEpoch: LocalEpoch - 1));
        DrainGameThread();

        Assert.Empty(ClaimedMachines());

        // The same decision stamped with the CURRENT epoch applies, proving the drop above is the
        // stale-epoch gate and not a blanket rejection.
        broker.Publish(this, new NetworkSiegeMachineAuthority(7, "us", hostEpoch: LocalEpoch));
        DrainGameThread();

        AssertSingleClaim(machineId: 7, controllerId: "us");
    }

    [Fact]
    [Trait("Requirement", "BR-102")]
    public void UnstampedAuthority_IsAccepted()
    {
        // Epoch 0 = the sender had no assignment yet; there is nothing to judge.
        broker.Publish(this, new NetworkSiegeMachineAuthority(8, "peer", hostEpoch: 0));
        DrainGameThread();

        AssertSingleClaim(machineId: 8, controllerId: "peer");
    }

    [Fact]
    [Trait("Requirement", "BR-102")]
    public void AuthorityAheadOfTheReceiver_IsAccepted()
    {
        // The NEW host's arbitration can arrive before this receiver's assignment broadcast does;
        // dropping it would silence the new host for the whole convergence window.
        broker.Publish(this, new NetworkSiegeMachineAuthority(9, "peer", hostEpoch: LocalEpoch + 1));
        DrainGameThread();

        AssertSingleClaim(machineId: 9, controllerId: "peer");
    }

    [Fact]
    [Trait("Requirement", "BR-102")]
    public void ReceiverWithoutAnAssignment_AcceptsStampedAuthority()
    {
        // We have not received the election result yet (local epoch 0) — we cannot judge staleness.
        session.SetupGet(s => s.HostEpoch).Returns(0);

        broker.Publish(this, new NetworkSiegeMachineAuthority(10, "peer", hostEpoch: 2));
        DrainGameThread();

        AssertSingleClaim(machineId: 10, controllerId: "peer");
    }

    // ------------------------------------------------------------------
    // NetworkSiegeMachineState — receiver gate
    // ------------------------------------------------------------------

    [Fact]
    [Trait("Requirement", "BR-102")]
    public void StaleEpochMachineState_IsDropped_AndCurrentEpochStateApplies()
    {
        // A state for a machine that has not registered locally is buffered for re-apply — the
        // handler's applied-vs-dropped seam. A deposed host's snapshot must not even be buffered.
        broker.Publish(this, MachineState(machineId: 21, hostEpoch: LocalEpoch - 1));
        DrainGameThread();

        Assert.Empty(PendingStates());

        broker.Publish(this, MachineState(machineId: 21, hostEpoch: LocalEpoch));
        DrainGameThread();

        var buffered = Assert.Single(PendingStates());
        Assert.Equal(21, buffered.Key);
        Assert.Equal(LocalEpoch, buffered.Value.HostEpoch);
    }

    [Fact]
    [Trait("Requirement", "BR-102")]
    public void UnstampedAndAheadMachineStates_AreAccepted()
    {
        // Epoch 0: a claimant broadcasting before the election result reached it (the live unstamped
        // case). Ahead: the sender heard about the migration first.
        broker.Publish(this, MachineState(machineId: 22, hostEpoch: 0));
        broker.Publish(this, MachineState(machineId: 23, hostEpoch: LocalEpoch + 1));
        DrainGameThread();

        var pending = PendingStates();
        Assert.Equal(2, pending.Count);
        Assert.True(pending.ContainsKey(22));
        Assert.True(pending.ContainsKey(23));
    }

    [Fact]
    [Trait("Requirement", "BR-102")]
    public void AfterAcceptingAHigherEpoch_ADelayedLowerButStillAheadMachineState_IsDropped()
    {
        // BR-102 accepted-epoch watermark (the reviewer's ordering scenario). This receiver is on
        // epoch 5 and accepts a snapshot from a newer generation (epoch 7), buffered for a machine that
        // has not registered locally yet. A delayed snapshot from the SUPERSEDED epoch-6 generation is
        // still ahead of the stored assignment (6 > 5), so the per-message assignment check ALONE would
        // accept it — its host-owned damage fields would then fight the promoted host's simulation. The
        // watermark raised to 7 by the first accept drops it before it is even buffered.
        //
        // Pre-fix (no watermark) the injected policy's IsStale(6, 5) returns false, machine 24's state
        // is buffered too, and PendingStates holds two entries — failing the single assertion below.
        broker.Publish(this, MachineState(machineId: 24, hostEpoch: LocalEpoch + 2));
        DrainGameThread();
        broker.Publish(this, MachineState(machineId: 25, hostEpoch: LocalEpoch + 1));
        DrainGameThread();

        var buffered = Assert.Single(PendingStates());
        Assert.Equal(24, buffered.Key);
        Assert.Equal(LocalEpoch + 2, buffered.Value.HostEpoch);
    }

    // ------------------------------------------------------------------
    // Sender stamping
    // ------------------------------------------------------------------

    [Fact]
    [Trait("Requirement", "BR-102")]
    public void HostArbitration_StampsTheAuthorityAnswerWithItsEpoch()
    {
        // The host arbitrating a peer's claim is THE host-authority act of this replicator; its
        // announcement must carry the arbitrating generation so late deliveries can be judged.
        session.SetupGet(s => s.IsLocalHost).Returns(true);

        broker.Publish(this, new NetworkSiegeMachineClaim(3, "peer", isRelease: false));
        DrainGameThread();

        var authority = Assert.IsType<NetworkSiegeMachineAuthority>(Assert.Single(sentToAll));
        Assert.Equal(3, authority.MachineId);
        Assert.Equal("peer", authority.ControllerId);
        Assert.Equal(LocalEpoch, authority.HostEpoch);
    }

    [Fact]
    [Trait("Requirement", "BR-102")]
    public void JoinerCatchUp_StampsTheReplayedAuthorityWithTheCurrentEpoch()
    {
        // Seed a claim as the host, then promote the epoch (a migration elsewhere in the mesh) and
        // catch a joiner up: the replay asserts authority NOW, so it carries the CURRENT epoch, not
        // the epoch the claim was minted under.
        missionScope.AsSiegeBattle();
        session.SetupGet(s => s.IsLocalHost).Returns(true);
        broker.Publish(this, new NetworkSiegeMachineClaim(3, "peer", isRelease: false));
        DrainGameThread();

        session.SetupGet(s => s.HostEpoch).Returns(LocalEpoch + 1);
        var sentToJoiner = new List<IMessage>();
        network.Setup(n => n.Send("joiner", It.IsAny<IMessage>()))
            .Callback<string, IMessage>((_, message) => sentToJoiner.Add(message));

        sut.CatchUpJoiner("joiner");
        DrainGameThread();

        var authority = Assert.IsType<NetworkSiegeMachineAuthority>(Assert.Single(sentToJoiner));
        Assert.Equal(3, authority.MachineId);
        Assert.Equal(LocalEpoch + 1, authority.HostEpoch);
    }

    [Fact]
    [Trait("Requirement", "BR-102")]
    public void OutgoingMachineState_IsStampedWithTheSendersEpoch_PreservingEveryField()
    {
        // Every outgoing NetworkSiegeMachineState (steady-state delta and join snapshot) passes the
        // send-boundary stamp, which asserts this sender's simulation authority NOW with the current
        // epoch. Machine capture itself needs live native machines, so the stamp is its own seam.
        // (A MissionObject test double is impossible here: materializing one runs the
        // ScriptComponentBehavior type initializer, which requires the native engine.)
        var method = typeof(SiegeMachineStateReplicator).GetMethod(
            "Stamp", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var captured = new NetworkSiegeMachineState(
            machineId: 30, hitPoints: 42.5f, destructionState: 2, gateState: 1, ladderState: 3,
            moveDistance: 18f, hasArrived: true, weaponState: 4, aimDirection: 0.75f,
            aimReleaseAngle: 0.25f);

        var stamped = Assert.IsType<NetworkSiegeMachineState>(
            method!.Invoke(sut, new object[] { captured }));

        Assert.Equal(LocalEpoch, stamped.HostEpoch);
        Assert.Equal(30, stamped.MachineId);
        Assert.Equal(42.5f, stamped.HitPoints);
        Assert.Equal(2, stamped.DestructionState);
        Assert.Equal(1, stamped.GateState);
        Assert.Equal(3, stamped.LadderState);
        Assert.Equal(18f, stamped.MoveDistance);
        Assert.True(stamped.HasArrived);
        Assert.Equal(4, stamped.WeaponState);
        Assert.Equal(0.75f, stamped.AimDirection);
        Assert.Equal(0.25f, stamped.AimReleaseAngle);
    }

    // ------------------------------------------------------------------
    // Plumbing
    // ------------------------------------------------------------------

    private static NetworkSiegeMachineState MachineState(int machineId, int hostEpoch)
        => new(machineId, hitPoints: -1f, destructionState: -1, gateState: -1, ladderState: -1,
            moveDistance: -1f, hasArrived: false, weaponState: -1, aimDirection: -1000f,
            aimReleaseAngle: -1000f, hostEpoch: hostEpoch);

    private Dictionary<int, string> ClaimedMachines()
        => GetField<Dictionary<int, string>>("claimedMachines");

    private void AssertSingleClaim(int machineId, string controllerId)
    {
        var claim = Assert.Single(ClaimedMachines());
        Assert.Equal(machineId, claim.Key);
        Assert.Equal(controllerId, claim.Value);
    }

    private Dictionary<int, NetworkSiegeMachineState> PendingStates()
        => GetField<Dictionary<int, NetworkSiegeMachineState>>("pendingByMachineId");

    private T GetField<T>(string fieldName)
    {
        var field = typeof(SiegeMachineStateReplicator).GetField(
            fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<T>(field!.GetValue(sut));
    }

    /// <summary>Handlers queue their bodies via <c>GameThread.RunSafe</c>; a blocking no-op queued
    /// after them (FIFO) proves they have run on the test game-loop pump before assertions read.</summary>
    private static void DrainGameThread() => GameThread.Run(() => { }, blocking: true);

    public void Dispose()
    {
        sut.Dispose();
        missionScope.Dispose();
        SiegeMissionAuthorityGate.ResetClaimedMachines();
    }
}
