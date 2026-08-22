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
using TaleWorlds.Library;
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
        session.Setup(s => s.IsHostController(It.IsAny<string>()))
            .Returns((string controllerId) => controllerId == "host");
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
    public void AuthorityAheadOfAStaleLocalHostAssignment_IsAccepted()
    {
        session.SetupGet(s => s.IsLocalHost).Returns(true);

        broker.Publish(this, new NetworkSiegeMachineAuthority(
            9, "peer", hostEpoch: LocalEpoch + 1, authorityRevision: 0,
            senderControllerId: "promoted-host"));
        DrainGameThread();

        AssertSingleClaim(machineId: 9, controllerId: "peer");
        Assert.Equal(LocalEpoch + 1, AuthorityEpochs()[9]);
        Assert.Equal("promoted-host", AuthorityHostControllers()[9]);
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

    [Fact]
    public void AuthorityMovingHere_InvalidatesThePreviousSimulatorSendCache()
    {
        broker.Publish(this, new NetworkSiegeMachineAuthority(
            11, "peer", hostEpoch: LocalEpoch, authorityRevision: 1));
        DrainGameThread();

        LastSentStates()[11] = MachineState(machineId: 11, hostEpoch: 0);
        LastSentLadderAnimations()[11] = LadderAnimationState(ladderId: 11, hostEpoch: 0);

        broker.Publish(this, new NetworkSiegeMachineAuthority(
            11, "us", hostEpoch: LocalEpoch, authorityRevision: 2));
        DrainGameThread();

        Assert.DoesNotContain(11, LastSentStates());
        Assert.DoesNotContain(11, LastSentLadderAnimations());
    }

    [Fact]
    public void NewAuthorityTupleForSameLocalOwner_InvalidatesPreviousEpochSendCache()
    {
        broker.Publish(this, new NetworkSiegeMachineAuthority(
            17, "us", hostEpoch: LocalEpoch, authorityRevision: 1,
            senderControllerId: "host"));
        DrainGameThread();

        LastSentStates()[17] = MachineState(machineId: 17, hostEpoch: LocalEpoch);
        LastSentLadderAnimations()[17] = LadderAnimationState(ladderId: 17, hostEpoch: LocalEpoch);

        broker.Publish(this, new NetworkSiegeMachineAuthority(
            17, "us", hostEpoch: LocalEpoch + 1, authorityRevision: 0,
            senderControllerId: "promoted-host"));
        DrainGameThread();

        Assert.DoesNotContain(17, LastSentStates());
        Assert.DoesNotContain(17, LastSentLadderAnimations());
    }

    [Fact]
    public void RemoteToRemoteAuthorityTransfer_RestampsUnchangedHostDamageForLoadingJoiner()
    {
        const int machineId = 18;
        session.SetupGet(s => s.IsLocalHost).Returns(true);

        var joinerBroker = new TestMessageBroker();
        var joinerSession = new Mock<IBattleSession>();
        joinerSession.SetupGet(s => s.InstanceId).Returns("mapEvent1");
        joinerSession.SetupGet(s => s.OwnControllerId).Returns("joiner");
        joinerSession.SetupGet(s => s.IsLocalHost).Returns(false);
        joinerSession.SetupGet(s => s.HostEpoch).Returns(LocalEpoch);
        joinerSession.Setup(s => s.IsHostController(It.IsAny<string>()))
            .Returns((string controllerId) => controllerId == "us");
        using var joiner = new SiegeMachineStateReplicator(
            new Mock<IBattleNetwork>().Object,
            joinerBroker,
            joinerSession.Object,
            new Mock<INetworkAgentRegistry>().Object,
            new HostEpochPolicy());

        InvokeSetMachineAuthority(machineId, "remote-a");
        var authorityA = Assert.IsType<NetworkSiegeMachineAuthority>(Assert.Single(sentToAll));
        Assert.Equal(1, authorityA.AuthorityRevision);
        joinerBroker.Publish(this, authorityA);
        DrainGameThread();
        sentToAll.Clear();

        var unchangedDamage = new NetworkSiegeMachineState(
            machineId,
            hitPoints: 42.5f,
            destructionState: 2,
            gateState: -1,
            ladderState: -1,
            moveDistance: -1f,
            hasArrived: false,
            weaponState: -1,
            aimDirection: -1000f,
            aimReleaseAngle: -1000f);
        InvokeBroadcastMachineStateIfChanged(unchangedDamage);

        var revisionOneDamage = Assert.IsType<NetworkSiegeMachineState>(Assert.Single(sentToAll));
        Assert.Equal(1, revisionOneDamage.AuthorityRevision);
        joinerBroker.Publish(this, revisionOneDamage);
        DrainGameThread();
        Assert.Equal(1, Assert.Single(PendingStates(joiner)).Value.AuthorityRevision);
        sentToAll.Clear();

        InvokeSetMachineAuthority(machineId, "remote-b");
        var authorityB = Assert.IsType<NetworkSiegeMachineAuthority>(Assert.Single(sentToAll));
        Assert.Equal("remote-b", authorityB.ControllerId);
        Assert.Equal(2, authorityB.AuthorityRevision);
        joinerBroker.Publish(this, authorityB);
        DrainGameThread();
        Assert.Empty(PendingStates(joiner));
        sentToAll.Clear();

        InvokeBroadcastMachineStateIfChanged(unchangedDamage);

        var revisionTwoDamage = Assert.IsType<NetworkSiegeMachineState>(Assert.Single(sentToAll));
        Assert.Equal(42.5f, revisionTwoDamage.HitPoints);
        Assert.Equal(2, revisionTwoDamage.DestructionState);
        Assert.Equal(LocalEpoch, revisionTwoDamage.HostEpoch);
        Assert.Equal(2, revisionTwoDamage.AuthorityRevision);
        Assert.Equal("us", revisionTwoDamage.SenderControllerId);
        joinerBroker.Publish(this, revisionTwoDamage);
        DrainGameThread();

        var buffered = Assert.Single(PendingStates(joiner)).Value;
        Assert.Equal(42.5f, buffered.HitPoints);
        Assert.Equal(2, buffered.DestructionState);
        Assert.Equal(2, buffered.AuthorityRevision);
    }

    [Fact]
    public void MountClaimAfterProximityGrant_RejectsRevokedOwnerGateHit()
    {
        const int machineId = 19;
        session.SetupGet(s => s.IsLocalHost).Returns(true);
        InvokePrivate("RefreshMachineCache");

        InvokeSetMachineAuthority(machineId, "remote-a");
        var authorityA = Assert.IsType<NetworkSiegeMachineAuthority>(Assert.Single(sentToAll));
        var hitFromA = new NetworkGateHit(
            gateId: 20,
            ramId: machineId,
            damage: 100,
            senderControllerId: authorityA.ControllerId,
            hostEpoch: authorityA.HostEpoch,
            authorityRevision: authorityA.AuthorityRevision);
        sentToAll.Clear();

        ProximityGrants().Add(machineId);
        broker.Publish(this, new NetworkSiegeMachineClaim(machineId, "remote-b", isRelease: false));
        DrainGameThread();

        var authorityB = Assert.IsType<NetworkSiegeMachineAuthority>(Assert.Single(sentToAll));
        Assert.Equal("remote-b", authorityB.ControllerId);
        Assert.Equal(authorityA.AuthorityRevision + 1, authorityB.AuthorityRevision);
        var hitFromB = new NetworkGateHit(
            gateId: 20,
            ramId: machineId,
            damage: 100,
            senderControllerId: authorityB.ControllerId,
            hostEpoch: authorityB.HostEpoch,
            authorityRevision: authorityB.AuthorityRevision);

        Assert.True(sut.TryGetMachineAuthority(
            machineId,
            out var currentControllerId,
            out var currentHostEpoch,
            out var currentAuthorityRevision));

        int appliedDamage = 0;
        foreach (var hit in new[] { hitFromA, hitFromB })
        {
            if (SiegeWeaponFireReplicator.IsCurrentGateHitProducer(
                    hit,
                    currentControllerId,
                    currentHostEpoch,
                    currentAuthorityRevision)
                && SiegeWeaponFireReplicator.ShouldApplyHostGateDamage(
                    isLocalHost: true,
                    ramSimulatedLocally: false))
            {
                appliedDamage += hit.Damage;
            }
        }

        Assert.Equal(hitFromB.Damage, appliedDamage);
    }

    [Fact]
    public void SupersededOwnerState_IsDropped_AndCannotReplaceAFutureRevision()
    {
        broker.Publish(this, new NetworkSiegeMachineAuthority(
            12, "owner-b", hostEpoch: LocalEpoch, authorityRevision: 2));
        DrainGameThread();

        broker.Publish(this, MachineState(
            12, LocalEpoch, senderControllerId: "owner-a", authorityRevision: 1));
        DrainGameThread();

        Assert.Empty(PendingStates());

        broker.Publish(this, MachineState(
            12, LocalEpoch, senderControllerId: "owner-c", authorityRevision: 3));
        broker.Publish(this, MachineState(
            12, LocalEpoch, senderControllerId: "owner-b", authorityRevision: 2));
        DrainGameThread();

        var pending = Assert.Single(PendingStates()).Value;
        Assert.Equal(3, pending.AuthorityRevision);
        Assert.Equal("owner-c", pending.SenderControllerId);

        broker.Publish(this, new NetworkSiegeMachineAuthority(
            12, "owner-c", hostEpoch: LocalEpoch, authorityRevision: 3));
        DrainGameThread();

        pending = Assert.Single(PendingStates()).Value;
        Assert.Equal(3, pending.AuthorityRevision);
        Assert.Equal("owner-c", pending.SenderControllerId);
    }

    [Fact]
    public void NewHostEpoch_ReplacesAHigherRevisionFromThePreviousHost()
    {
        broker.Publish(this, new NetworkSiegeMachineAuthority(
            13, "owner-a", hostEpoch: LocalEpoch, authorityRevision: 8,
            senderControllerId: "host"));
        DrainGameThread();

        broker.Publish(this, new NetworkSiegeMachineAuthority(
            13, "owner-b", hostEpoch: LocalEpoch + 1, authorityRevision: 1,
            senderControllerId: "promoted-host"));
        DrainGameThread();

        AssertSingleClaim(13, "owner-b");
        Assert.Equal(LocalEpoch + 1, AuthorityEpochs()[13]);
        Assert.Equal(1, AuthorityRevisions()[13]);
        Assert.Equal("promoted-host", AuthorityHostControllers()[13]);
    }

    [Fact]
    public void PromotedHost_NormalizesAnInheritedClaimBeforeAuthorityLookup()
    {
        const int machineId = 31;
        InvokeSetMachineAuthority(machineId, "remote-a");
        sentToAll.Clear();

        session.SetupGet(s => s.IsLocalHost).Returns(true);
        session.SetupGet(s => s.HostEpoch).Returns(LocalEpoch + 1);
        int changedMachineId = -1;
        sut.AuthorityChanged += machineId => changedMachineId = machineId;

        Assert.True(sut.TryGetMachineAuthority(
            machineId,
            out var controllerId,
            out var hostEpoch,
            out var authorityRevision));

        Assert.Equal("remote-a", controllerId);
        Assert.Equal(LocalEpoch + 1, hostEpoch);
        Assert.Equal(0, authorityRevision);

        var authority = Assert.IsType<NetworkSiegeMachineAuthority>(Assert.Single(sentToAll));
        Assert.Equal(machineId, authority.MachineId);
        Assert.Equal("remote-a", authority.ControllerId);
        Assert.Equal(LocalEpoch + 1, authority.HostEpoch);
        Assert.Equal(0, authority.AuthorityRevision);
        Assert.Equal("us", authority.SenderControllerId);
        Assert.Equal(machineId, changedMachineId);

        var oldEpochHit = new NetworkGateHit(
            gateId: 32,
            ramId: machineId,
            damage: 100,
            senderControllerId: "remote-a",
            hostEpoch: LocalEpoch,
            authorityRevision: 1);
        Assert.False(SiegeWeaponFireReplicator.IsCurrentGateHitProducer(
            oldEpochHit,
            controllerId,
            hostEpoch,
            authorityRevision));
    }

    [Fact]
    public void PromotedHostSnapshot_BuffersUntilItsAuthorityDecisionArrives()
    {
        broker.Publish(this, MachineState(
            14,
            LocalEpoch + 1,
            senderControllerId: "promoted-host",
            authorityRevision: 0));
        DrainGameThread();

        var pending = Assert.Single(PendingStates()).Value;
        Assert.Equal("promoted-host", pending.SenderControllerId);

        broker.Publish(this, new NetworkSiegeMachineAuthority(
            14, string.Empty, hostEpoch: LocalEpoch + 1, authorityRevision: 0,
            senderControllerId: "promoted-host"));
        DrainGameThread();

        pending = Assert.Single(PendingStates()).Value;
        Assert.Equal("promoted-host", pending.SenderControllerId);
        Assert.Equal(LocalEpoch + 1, AuthorityEpochs()[14]);
        Assert.Equal("promoted-host", AuthorityHostControllers()[14]);
    }

    [Fact]
    public void NewerEpochPendingState_ReplacesHigherRevisionFromPreviousEpoch()
    {
        broker.Publish(this, new NetworkSiegeMachineAuthority(
            15, "owner-a", hostEpoch: LocalEpoch, authorityRevision: 8,
            senderControllerId: "host"));
        broker.Publish(this, MachineState(
            15, LocalEpoch, senderControllerId: "owner-a", authorityRevision: 8));
        broker.Publish(this, MachineState(
            15, LocalEpoch + 1, senderControllerId: "owner-b", authorityRevision: 0));
        DrainGameThread();

        var pending = Assert.Single(PendingStates()).Value;
        Assert.Equal(LocalEpoch + 1, pending.HostEpoch);
        Assert.Equal(0, pending.AuthorityRevision);
        Assert.Equal("owner-b", pending.SenderControllerId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EqualAuthorityHostAndClaimantSnapshots_MergeBeforeMachineRegistration(bool hostFirst)
    {
        const int machineId = 18;
        const int authorityRevision = 4;
        broker.Publish(this, new NetworkSiegeMachineAuthority(
            machineId, "claimant", hostEpoch: LocalEpoch, authorityRevision: authorityRevision,
            senderControllerId: "host"));
        DrainGameThread();

        var hostState = new NetworkSiegeMachineState(
            machineId,
            hitPoints: 42.5f,
            destructionState: 2,
            gateState: -1,
            ladderState: -1,
            moveDistance: -1f,
            hasArrived: false,
            weaponState: -1,
            aimDirection: -1000f,
            aimReleaseAngle: -1000f,
            hostEpoch: LocalEpoch,
            senderControllerId: "host",
            authorityRevision: authorityRevision);
        var claimantState = new NetworkSiegeMachineState(
            machineId,
            hitPoints: -1f,
            destructionState: -1,
            gateState: 1,
            ladderState: 3,
            moveDistance: 18f,
            hasArrived: true,
            weaponState: 4,
            aimDirection: 0.75f,
            aimReleaseAngle: 0.25f,
            hostEpoch: LocalEpoch,
            stoneAmmo: 7,
            senderControllerId: "claimant",
            authorityRevision: authorityRevision);

        broker.Publish(this, hostFirst ? hostState : claimantState);
        broker.Publish(this, hostFirst ? claimantState : hostState);
        DrainGameThread();

        var pending = Assert.Single(PendingStates()).Value;
        Assert.Equal(42.5f, pending.HitPoints);
        Assert.Equal(2, pending.DestructionState);
        Assert.Equal(1, pending.GateState);
        Assert.Equal(3, pending.LadderState);
        Assert.Equal(18f, pending.MoveDistance);
        Assert.True(pending.HasArrived);
        Assert.Equal(4, pending.WeaponState);
        Assert.Equal(0.75f, pending.AimDirection);
        Assert.Equal(0.25f, pending.AimReleaseAngle);
        Assert.True(pending.HasStoneAmmo);
        Assert.Equal(7, pending.StoneAmmo);
        Assert.Equal(LocalEpoch, pending.HostEpoch);
        Assert.Equal(authorityRevision, pending.AuthorityRevision);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EqualAuthorityHostAndClaimantSnapshots_MergeWhenMachineRegistersBetweenArrivals(bool hostFirst)
    {
        const int machineId = 19;
        const int authorityRevision = 4;
        broker.Publish(this, new NetworkSiegeMachineAuthority(
            machineId, "claimant", hostEpoch: LocalEpoch, authorityRevision: authorityRevision,
            senderControllerId: "host"));
        DrainGameThread();

        var hostState = new NetworkSiegeMachineState(
            machineId,
            hitPoints: 42.5f,
            destructionState: 2,
            gateState: -1,
            ladderState: -1,
            moveDistance: -1f,
            hasArrived: false,
            weaponState: -1,
            aimDirection: -1000f,
            aimReleaseAngle: -1000f,
            hostEpoch: LocalEpoch,
            senderControllerId: "host",
            authorityRevision: authorityRevision);
        var claimantState = new NetworkSiegeMachineState(
            machineId,
            hitPoints: -1f,
            destructionState: -1,
            gateState: 1,
            ladderState: 3,
            moveDistance: 18f,
            hasArrived: true,
            weaponState: 4,
            aimDirection: 0.75f,
            aimReleaseAngle: 0.25f,
            hostEpoch: LocalEpoch,
            stoneAmmo: 7,
            senderControllerId: "claimant",
            authorityRevision: authorityRevision);

        broker.Publish(this, hostFirst ? hostState : claimantState);
        DrainGameThread();

        // The native machine registers before the second arrival. This is the exact pending-to-live
        // reconciliation seam used by the handler after its refreshed machine lookup succeeds.
        var stateToApply = ReconcilePendingStateForLiveApply(hostFirst ? claimantState : hostState);

        Assert.Empty(PendingStates());
        Assert.Equal(42.5f, stateToApply.HitPoints);
        Assert.Equal(2, stateToApply.DestructionState);
        Assert.Equal(1, stateToApply.GateState);
        Assert.Equal(3, stateToApply.LadderState);
        Assert.Equal(18f, stateToApply.MoveDistance);
        Assert.True(stateToApply.HasArrived);
        Assert.Equal(4, stateToApply.WeaponState);
        Assert.Equal(0.75f, stateToApply.AimDirection);
        Assert.Equal(0.25f, stateToApply.AimReleaseAngle);
        Assert.True(stateToApply.HasStoneAmmo);
        Assert.Equal(7, stateToApply.StoneAmmo);
        Assert.Equal(LocalEpoch, stateToApply.HostEpoch);
        Assert.Equal(authorityRevision, stateToApply.AuthorityRevision);
    }

    [Fact]
    public void OlderPendingMachineState_IsDiscardedWhenLiveArrivalHasNewerAuthority()
    {
        PendingStates()[20] = MachineState(
            20, LocalEpoch, senderControllerId: "old-owner", authorityRevision: 7);
        var incoming = MachineState(
            20, LocalEpoch + 1, senderControllerId: "new-owner", authorityRevision: 0);

        var stateToApply = ReconcilePendingStateForLiveApply(incoming);

        Assert.Same(incoming, stateToApply);
        Assert.Empty(PendingStates());
    }

    [Fact]
    public void NewerPendingMachineState_IsPreservedWhenLiveArrivalHasOlderAuthority()
    {
        var pending = MachineState(
            21, LocalEpoch + 1, senderControllerId: "new-owner", authorityRevision: 0);
        PendingStates()[21] = pending;
        var incoming = MachineState(
            21, LocalEpoch, senderControllerId: "old-owner", authorityRevision: 7);

        var stateToApply = ReconcilePendingStateForLiveApply(incoming);

        Assert.Same(incoming, stateToApply);
        Assert.Same(pending, Assert.Single(PendingStates()).Value);
    }

    [Fact]
    public void NewerEpochPendingLadderAnimation_ReplacesHigherRevisionFromPreviousEpoch()
    {
        broker.Publish(this, new NetworkSiegeMachineAuthority(
            16, "owner-a", hostEpoch: LocalEpoch, authorityRevision: 8,
            senderControllerId: "host"));
        broker.Publish(this, LadderAnimationState(
            16, LocalEpoch, senderControllerId: "owner-a", authorityRevision: 8));
        broker.Publish(this, LadderAnimationState(
            16, LocalEpoch + 1, senderControllerId: "owner-b", authorityRevision: 0));
        DrainGameThread();

        var pending = Assert.Single(PendingLadderAnimations()).Value;
        Assert.Equal(LocalEpoch + 1, pending.HostEpoch);
        Assert.Equal(0, pending.AuthorityRevision);
        Assert.Equal("owner-b", pending.SenderControllerId);
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

    [Fact]
    public void PendingAnimationAfterDiscreteState_IsRetainedForOrderedApply()
    {
        broker.Publish(this, MachineState(machineId: 26, hostEpoch: LocalEpoch));
        broker.Publish(this, LadderAnimationState(ladderId: 26, hostEpoch: LocalEpoch));
        DrainGameThread();

        Assert.True(PendingStates().ContainsKey(26));
        Assert.True(PendingLadderAnimations().ContainsKey(26));
    }

    [Fact]
    public void LaterDiscreteState_RetainsTheLatestPendingAnimation()
    {
        broker.Publish(this, LadderAnimationState(ladderId: 27, hostEpoch: LocalEpoch));
        broker.Publish(this, MachineState(machineId: 27, hostEpoch: LocalEpoch));
        DrainGameThread();

        Assert.True(PendingStates().ContainsKey(27));
        Assert.True(PendingLadderAnimations().ContainsKey(27));
    }

    [Fact]
    [Trait("Requirement", "BR-102")]
    public void PendingAnimationFromASupersededEpoch_IsDroppedBeforeRegistration()
    {
        broker.Publish(this, LadderAnimationState(ladderId: 28, hostEpoch: LocalEpoch));
        broker.Publish(this, LadderAnimationState(ladderId: 29, hostEpoch: LocalEpoch + 1));
        DrainGameThread();

        InvokePrivate("DrainPendingMachineStates");

        var pending = Assert.Single(PendingLadderAnimations());
        Assert.Equal(29, pending.Key);
        Assert.Equal(LocalEpoch + 1, pending.Value.HostEpoch);
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
        Assert.Equal(1, authority.AuthorityRevision);
        Assert.Equal("us", authority.SenderControllerId);
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
        Assert.Equal(0, authority.AuthorityRevision);
        Assert.Equal("us", authority.SenderControllerId);
    }

    [Fact]
    public void StaleLocalHostAssignment_DoesNotCatchUpAJoinerFromAnAheadAuthorityTuple()
    {
        missionScope.AsSiegeBattle();
        session.SetupGet(s => s.IsLocalHost).Returns(true);
        broker.Publish(this, new NetworkSiegeMachineAuthority(
            4, "peer", hostEpoch: LocalEpoch + 1, authorityRevision: 0,
            senderControllerId: "promoted-host"));
        DrainGameThread();

        var sentToJoiner = new List<IMessage>();
        network.Setup(n => n.Send("joiner", It.IsAny<IMessage>()))
            .Callback<string, IMessage>((_, message) => sentToJoiner.Add(message));

        sut.CatchUpJoiner("joiner");
        DrainGameThread();

        Assert.Empty(sentToJoiner);
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
            "Stamp",
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            new[] { typeof(NetworkSiegeMachineState) },
            null);
        Assert.NotNull(method);

        var captured = new NetworkSiegeMachineState(
            machineId: 30, hitPoints: 42.5f, destructionState: 2, gateState: 1, ladderState: 3,
            moveDistance: 18f, hasArrived: true, weaponState: 4, aimDirection: 0.75f,
            aimReleaseAngle: 0.25f, stoneAmmo: 7);
        AuthorityRevisions()[30] = 3;
        AuthorityEpochs()[30] = LocalEpoch + 1;

        var stamped = Assert.IsType<NetworkSiegeMachineState>(
            method!.Invoke(sut, new object[] { captured }));

        Assert.Equal(LocalEpoch + 1, stamped.HostEpoch);
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
        Assert.True(stamped.HasStoneAmmo);
        Assert.Equal(7, stamped.StoneAmmo);
        Assert.Equal("us", stamped.SenderControllerId);
        Assert.Equal(3, stamped.AuthorityRevision);
    }

    [Fact]
    [Trait("Requirement", "BR-102")]
    public void OutgoingLadderAnimationState_IsStampedWithTheSendersEpoch_PreservingEveryField()
    {
        var method = typeof(SiegeMachineStateReplicator).GetMethod(
            "Stamp",
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            new[] { typeof(NetworkSiegeLadderAnimationState) },
            null);
        Assert.NotNull(method);

        var frame = new MatrixFrame(Mat3.Identity, new Vec3(1f, 2f, 3f));
        var captured = new NetworkSiegeLadderAnimationState(
            ladderId: 30,
            animationSpeed: 1.73f,
            animationProgress: 0.42f,
            animationState: 2,
            fallAngularSpeed: -0.5f,
            frame: frame,
            animationIndex: 17);
        AuthorityRevisions()[30] = 3;
        AuthorityEpochs()[30] = LocalEpoch + 1;

        var stamped = Assert.IsType<NetworkSiegeLadderAnimationState>(
            method!.Invoke(sut, new object[] { captured }));

        Assert.Equal(LocalEpoch + 1, stamped.HostEpoch);
        Assert.Equal(30, stamped.LadderId);
        Assert.Equal(1.73f, stamped.AnimationSpeed);
        Assert.Equal(0.42f, stamped.AnimationProgress);
        Assert.Equal(2, stamped.AnimationState);
        Assert.Equal(-0.5f, stamped.FallAngularSpeed);
        Assert.Equal(frame.origin, stamped.Frame.origin);
        Assert.Equal(17, stamped.AnimationIndex);
        Assert.Equal("us", stamped.SenderControllerId);
        Assert.Equal(3, stamped.AuthorityRevision);
    }

    // ------------------------------------------------------------------
    // Plumbing
    // ------------------------------------------------------------------

    private static NetworkSiegeMachineState MachineState(
        int machineId,
        int hostEpoch,
        string senderControllerId = "host",
        int authorityRevision = 0)
        => new(machineId, hitPoints: -1f, destructionState: -1, gateState: -1, ladderState: -1,
            moveDistance: -1f, hasArrived: false, weaponState: -1, aimDirection: -1000f,
            aimReleaseAngle: -1000f, hostEpoch: hostEpoch,
            senderControllerId: senderControllerId, authorityRevision: authorityRevision);

    private static NetworkSiegeLadderAnimationState LadderAnimationState(
        int ladderId,
        int hostEpoch,
        string senderControllerId = "host",
        int authorityRevision = 0)
        => new(ladderId, animationSpeed: -1f, animationProgress: -1f, animationState: 0,
            fallAngularSpeed: 0f, frame: MatrixFrame.Identity, animationIndex: -1,
            hostEpoch: hostEpoch, senderControllerId: senderControllerId,
            authorityRevision: authorityRevision);

    private Dictionary<int, string> ClaimedMachines()
        => GetField<Dictionary<int, string>>("claimedMachines");

    private Dictionary<int, int> AuthorityRevisions()
        => GetField<Dictionary<int, int>>("authorityRevisions");

    private Dictionary<int, int> AuthorityEpochs()
        => GetField<Dictionary<int, int>>("authorityEpochs");

    private Dictionary<int, string> AuthorityHostControllers()
        => GetField<Dictionary<int, string>>("authorityHostControllers");

    private HashSet<int> ProximityGrants()
        => GetField<HashSet<int>>("proximityGrants");

    private void AssertSingleClaim(int machineId, string controllerId)
    {
        var claim = Assert.Single(ClaimedMachines());
        Assert.Equal(machineId, claim.Key);
        Assert.Equal(controllerId, claim.Value);
    }

    private Dictionary<int, NetworkSiegeMachineState> PendingStates()
        => PendingStates(sut);

    private static Dictionary<int, NetworkSiegeMachineState> PendingStates(
        SiegeMachineStateReplicator replicator)
        => GetField<Dictionary<int, NetworkSiegeMachineState>>(replicator, "pendingByMachineId");

    private NetworkSiegeMachineState ReconcilePendingStateForLiveApply(NetworkSiegeMachineState state)
    {
        var method = typeof(SiegeMachineStateReplicator).GetMethod(
            "ReconcilePendingMachineStateForLiveApply", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<NetworkSiegeMachineState>(method!.Invoke(sut, new object[] { state }));
    }

    private Dictionary<int, NetworkSiegeLadderAnimationState> PendingLadderAnimations()
        => GetField<Dictionary<int, NetworkSiegeLadderAnimationState>>("pendingLadderAnimationsById");

    private Dictionary<int, NetworkSiegeMachineState> LastSentStates()
        => GetField<Dictionary<int, NetworkSiegeMachineState>>("lastSent");

    private Dictionary<int, NetworkSiegeLadderAnimationState> LastSentLadderAnimations()
        => GetField<Dictionary<int, NetworkSiegeLadderAnimationState>>("lastSentLadderAnimations");

    private void InvokePrivate(string methodName)
    {
        var method = typeof(SiegeMachineStateReplicator).GetMethod(
            methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(sut, Array.Empty<object>());
    }

    private void InvokeSetMachineAuthority(int machineId, string controllerId)
    {
        var method = typeof(SiegeMachineStateReplicator).GetMethod(
            "SetMachineAuthority", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(sut, new object[] { machineId, controllerId });
    }

    private void InvokeBroadcastMachineStateIfChanged(NetworkSiegeMachineState state)
    {
        var method = typeof(SiegeMachineStateReplicator).GetMethod(
            "BroadcastMachineStateIfChanged", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(sut, new object[] { state });
    }

    private T GetField<T>(string fieldName)
        => GetField<T>(sut, fieldName);

    private static T GetField<T>(SiegeMachineStateReplicator replicator, string fieldName)
    {
        var field = typeof(SiegeMachineStateReplicator).GetField(
            fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<T>(field!.GetValue(replicator));
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
