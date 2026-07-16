using Common;
using Common.Messaging;
using Common.Tests.Utils;
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
/// BR-102 for the engine-placement mesh traffic of <see cref="SiegeEngineDeploymentReplicator"/>:
/// <see cref="NetworkSiegeEnginePlacement"/> is host authority (only the mission host deploys for
/// both sides) and is order-sensitive — every transition is recorded as authoritative history and
/// replayed to joiners — so a deposed host's in-flight placement must be dropped before it enters
/// that history. The sender stamps its epoch on live placements and re-stamps its CURRENT epoch on
/// joiner catch-up resends; receivers accept unstamped (0) and ahead-of-receiver epochs per
/// <see cref="HostEpochPolicy"/>.
/// </summary>
[Collection("Mission.Current")]
public class SiegeEngineDeploymentReplicatorHostEpochTests : IDisposable
{
    private const int LocalEpoch = 5;

    private readonly TestMessageBroker broker = new();
    private readonly Mock<IBattleNetwork> network = new();
    private readonly Mock<IBattleSession> session = new();
    private readonly List<IMessage> sentToAll = new();
    private readonly MissionCurrentScope missionScope = new();
    private readonly SiegeEngineDeploymentReplicator sut;

    public SiegeEngineDeploymentReplicatorHostEpochTests()
    {
        session.SetupGet(s => s.InstanceId).Returns("mapEvent1");
        session.SetupGet(s => s.OwnControllerId).Returns("us");
        session.SetupGet(s => s.IsLocalHost).Returns(false);
        session.SetupGet(s => s.HostEpoch).Returns(LocalEpoch);
        network.Setup(n => n.SendAll(It.IsAny<IMessage>())).Callback<IMessage>(sentToAll.Add);

        sut = new SiegeEngineDeploymentReplicator(network.Object, broker, session.Object, new HostEpochPolicy());
    }

    // ------------------------------------------------------------------
    // Receiver gate
    // ------------------------------------------------------------------

    [Fact]
    [Trait("Requirement", "BR-102")]
    public void StaleEpochPlacement_IsDroppedBeforeTheHistory_AndCurrentEpochPlacementApplies()
    {
        // A deposed host's in-flight placement must not enter the authoritative transition history
        // (it would be replayed to every later joiner) nor the apply queue.
        broker.Publish(this, new NetworkSiegeEnginePlacement(1, "Ballista", hostEpoch: LocalEpoch - 1));
        DrainGameThread();

        Assert.Empty(Transitions("placements"));
        Assert.Empty(Transitions("pending"));

        // The same placement stamped with the CURRENT epoch is recorded and queued, proving the drop
        // above is the stale-epoch gate and not a blanket rejection.
        broker.Publish(this, new NetworkSiegeEnginePlacement(1, "Ballista", hostEpoch: LocalEpoch));
        DrainGameThread();

        AssertSingleTransition(Transitions("placements"), 1, "Ballista");
        AssertSingleTransition(Transitions("pending"), 1, "Ballista");
    }

    [Fact]
    [Trait("Requirement", "BR-102")]
    public void UnstampedAndAheadPlacements_AreAccepted()
    {
        // Epoch 0 = unstamped sender (no assignment yet); ahead = the sender heard about the
        // migration before this receiver did. Both must apply or the live deployer goes silent.
        broker.Publish(this, new NetworkSiegeEnginePlacement(2, "Mangonel", hostEpoch: 0));
        broker.Publish(this, new NetworkSiegeEnginePlacement(3, "Trebuchet", hostEpoch: LocalEpoch + 1));
        DrainGameThread();

        var history = Transitions("placements");
        Assert.Equal(2, history.Count);
        Assert.Equal(new KeyValuePair<int, string>(2, "Mangonel"), history[0]);
        Assert.Equal(new KeyValuePair<int, string>(3, "Trebuchet"), history[1]);
    }

    [Fact]
    [Trait("Requirement", "BR-102")]
    public void ReceiverWithoutAnAssignment_AcceptsStampedPlacements()
    {
        // No assignment received yet (local epoch 0): staleness cannot be judged; accept.
        session.SetupGet(s => s.HostEpoch).Returns(0);

        broker.Publish(this, new NetworkSiegeEnginePlacement(4, "Ballista", hostEpoch: 2));
        DrainGameThread();

        AssertSingleTransition(Transitions("placements"), 4, "Ballista");
    }

    [Fact]
    [Trait("Requirement", "BR-102")]
    public void AfterAcceptingAHigherEpoch_ADelayedLowerButStillAheadPlacement_IsDroppedBeforeTheHistory()
    {
        // BR-102 accepted-epoch watermark (the reviewer's ordering scenario). This receiver is on
        // epoch 5 and accepts a placement from a newer generation (epoch 7). A delayed placement from
        // the SUPERSEDED epoch-6 generation is still ahead of the stored assignment (6 > 5), so the
        // per-message assignment check ALONE would accept it and append the older placement to the
        // authoritative history joiners are replayed from — applying it LAST. The watermark raised to 7
        // by the first accept drops it.
        //
        // Pre-fix (no watermark) the injected policy's IsStale(6, 5) returns false, the delayed
        // placement is recorded, and Transitions("placements") holds two entries — failing the single
        // assertion below.
        broker.Publish(this, new NetworkSiegeEnginePlacement(1, "Ballista", hostEpoch: LocalEpoch + 2));
        DrainGameThread();
        broker.Publish(this, new NetworkSiegeEnginePlacement(2, "Mangonel", hostEpoch: LocalEpoch + 1));
        DrainGameThread();

        // Only the epoch-7 placement entered the history and the pending queue; the delayed epoch-6 one
        // was dropped before both.
        AssertSingleTransition(Transitions("placements"), 1, "Ballista");
        AssertSingleTransition(Transitions("pending"), 1, "Ballista");
    }

    // ------------------------------------------------------------------
    // Sender stamping
    // ------------------------------------------------------------------

    [Fact]
    [Trait("Requirement", "BR-102")]
    public void HostPlacementBroadcast_IsStampedWithTheSendersEpoch()
    {
        // The host's live placement pipeline (Handle_PlacementChanged) funnels into this seam; the
        // local capture message itself carries a native DeploymentPoint that cannot be materialized
        // headless (its ScriptComponentBehavior type initializer needs the engine).
        Invoke("BroadcastPlacement", 7, "Ballista");

        var placement = Assert.IsType<NetworkSiegeEnginePlacement>(Assert.Single(sentToAll));
        Assert.Equal(7, placement.PointId);
        Assert.Equal("Ballista", placement.WeaponTypeName);
        Assert.Equal(LocalEpoch, placement.HostEpoch);

        // The broadcast transition also entered the authoritative history joiners are caught up from.
        AssertSingleTransition(Transitions("placements"), 7, "Ballista");
    }

    [Fact]
    [Trait("Requirement", "BR-102")]
    public void JoinerCatchUp_ReStampsResendsWithTheCurrentEpoch()
    {
        // A placement recorded under epoch 5, replayed after a promotion chain moved us to epoch 6:
        // the catch-up resend asserts deployment authority NOW, so it carries the CURRENT epoch, not
        // the epoch the transition was minted under — otherwise a joiner holding the newer assignment
        // would drop the promoted successor's replay.
        Invoke("Record", 7, "Ballista");
        session.SetupGet(s => s.IsLocalHost).Returns(true);
        session.SetupGet(s => s.HostEpoch).Returns(LocalEpoch + 1);

        var sentToJoiner = new List<IMessage>();
        network.Setup(n => n.Send("joiner", It.IsAny<IMessage>()))
            .Callback<string, IMessage>((_, message) => sentToJoiner.Add(message));

        sut.CatchUpJoiner("joiner");
        DrainGameThread();

        var resent = Assert.IsType<NetworkSiegeEnginePlacement>(Assert.Single(sentToJoiner));
        Assert.Equal("Ballista", resent.WeaponTypeName);
        Assert.Equal(LocalEpoch + 1, resent.HostEpoch);
    }

    // ------------------------------------------------------------------
    // Plumbing
    // ------------------------------------------------------------------

    private void Invoke(string methodName, params object[] arguments)
    {
        var method = typeof(SiegeEngineDeploymentReplicator).GetMethod(
            methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(sut, arguments);
    }

    private List<KeyValuePair<int, string>> Transitions(string fieldName)
    {
        var field = typeof(SiegeEngineDeploymentReplicator).GetField(
            fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<List<KeyValuePair<int, string>>>(field!.GetValue(sut));
    }

    private static void AssertSingleTransition(List<KeyValuePair<int, string>> transitions, int pointId, string weaponType)
    {
        var transition = Assert.Single(transitions);
        Assert.Equal(pointId, transition.Key);
        Assert.Equal(weaponType, transition.Value);
    }

    /// <summary>Handlers queue their bodies via <c>GameThread.RunSafe</c>; a blocking no-op queued
    /// after them (FIFO) proves they have run on the test game-loop pump before assertions read.</summary>
    private static void DrainGameThread() => GameThread.Run(() => { }, blocking: true);

    public void Dispose()
    {
        sut.Dispose();
        missionScope.Dispose();
    }
}
