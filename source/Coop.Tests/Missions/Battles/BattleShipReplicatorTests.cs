using Common;
using Common.Messaging;
using GameInterface.Services.ObjectManager;
using Missions;
using Missions.Agents.Handlers;
using Missions.Battles;
using Missions.Messages;
using Missions.Services.Network;
using Moq;
using Newtonsoft.Json.Linq;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

[Collection("Mission.Current")]
public class BattleShipReplicatorTests
{
    private const string Own = "local";
    private const string Peer = "peer";
    private const string Instance = "MapEvent_1";
    private const int HostEpoch = 3;
    private static readonly string[] InMission = { Peer, "someone_else", "third_peer", "successor" };

    [Fact]
    public void Tick_BeforeDeploymentCommit_WithholdsOwnHullRecords()
    {
        var harness = new Harness(committed: false);

        harness.Replicator.Tick(0.1f);

        harness.Network.Verify(n => n.SendAll(It.IsAny<IMessage>()), Times.Never);
    }

    [Fact]
    public void Tick_AfterDeploymentCommit_SendsOwnHullRecordsOnceThenSamples()
    {
        var harness = new Harness(committed: true);

        harness.Replicator.Tick(0.1f);
        harness.Replicator.Tick(0.1f);

        harness.Network.Verify(n => n.SendAll(It.Is<IMessage>(m =>
            m is NetworkSpawnBattleShips && ((NetworkSpawnBattleShips)m).Ships.Length == 1)), Times.Once);
        harness.Network.Verify(n => n.SendAll(It.Is<IMessage>(m =>
            m is NetworkBattleShipSample && ((NetworkBattleShipSample)m).OwnerControllerId == Own)), Times.Exactly(2));
    }

    [Fact]
    public void Tick_AfterDeploymentCommit_AnnouncesEveryOwnHullOfAFleetInOneRecordSet()
    {
        var harness = new Harness(committed: true);
        var second = CreateHull();
        var third = CreateHull();
        harness.Registry.TryRegister(new NetworkShipInfo(Guid.NewGuid(), Own, "MapEventParty_1", false, second, null));
        harness.Registry.TryRegister(new NetworkShipInfo(Guid.NewGuid(), Own, "MapEventParty_1", false, third, null));
        harness.LiveHulls.Add(second);
        harness.LiveHulls.Add(third);

        harness.Replicator.Tick(0.1f);

        harness.Network.Verify(n => n.SendAll(It.Is<IMessage>(m =>
            m is NetworkSpawnBattleShips && ((NetworkSpawnBattleShips)m).Ships.Length == 3)), Times.Once);
        harness.Network.Verify(n => n.SendAll(It.Is<IMessage>(m =>
            m is NetworkBattleShipSample && ((NetworkBattleShipSample)m).OwnerControllerId == Own)), Times.Exactly(3));
    }

    [Theory]
    [InlineData(Peer, Peer, 5, 0, null)]
    [InlineData(Peer, "someone_else", 5, 0, "not_authority")]
    [InlineData(Peer, Peer, 5, 5, "stale")]
    [InlineData(Own, Own, 5, 0, "own_hull")]
    public void ValidateSample_AcceptsOnlyNewerSamplesFromTheHullAuthority(
        string authority, string sender, long sequence, long accepted, string expected)
    {
        var ship = new NetworkShipInfo(Guid.NewGuid(), authority, null, false, CreateHull(), null);
        var sample = new NetworkBattleShipSample(ship.ShipId, sender, sequence, NetworkBattleShipSample.FromFrame(MatrixFrame.Identity));

        Assert.Equal(expected, BattleShipReplicator.ValidateSample(ship, Own, sample, accepted, 0, InMission));
    }

    [Fact]
    public void AcceptSample_ExpiresTheTargetOnlyAfterItsLifetimeOnThisClientsClock()
    {
        var harness = new Harness(committed: false);
        var copy = CreateHull();
        var ship = new NetworkShipInfo(Guid.NewGuid(), Peer, "MapEventParty_2", false, copy, null);
        harness.Registry.TryRegister(ship);
        harness.LiveHulls.Add(copy);
        var frame = NetworkBattleShipSample.FromFrame(MatrixFrame.Identity);

        harness.Replicator.AcceptSample(new NetworkBattleShipSample(ship.ShipId, Peer, 1, frame));
        harness.Replicator.Tick(0.8f);
        harness.Replicator.AcceptSample(new NetworkBattleShipSample(ship.ShipId, Peer, 2, frame));
        harness.Replicator.Tick(0.8f);
        bool streamingWhileFresh = (bool)Inspected(harness, ship.ShipId)["streaming"]!;
        harness.Replicator.Tick(0.3f);

        var inspected = Inspected(harness, ship.ShipId);
        Assert.True(streamingWhileFresh);
        Assert.False((bool)inspected["streaming"]!);
        Assert.Equal("expired", (string)inspected["lastReject"]!);
        Assert.Equal(2, (long)inspected["accepted"]!);
        harness.Engine.Verify(e => e.ApplyForeignSample(copy, It.IsAny<MatrixFrame>(), It.IsAny<BattleShipMotion>()), Times.Exactly(2));
    }

    // K4, R7: only a hull's owner tracks its reserve queue, so it rides the hull's on-change condition to the battle host's
    // end check; the 20 Hz sample no longer carries it, and an unchanged reserve sends nothing more.
    [Fact]
    public void Tick_OwnConditionCarriesTheHullsSpawnableReserve_AndSendsAgainOnlyWhenItChanges()
    {
        var harness = new Harness(committed: true);
        harness.Engine.Setup(e => e.ReadCondition(harness.OwnHull)).Returns(Condition(100f));
        int reserve = 7;
        harness.Engine.Setup(e => e.ReadSpawnableReserve(harness.OwnHull)).Returns(() => reserve);

        harness.Replicator.Tick(0.1f);
        harness.Replicator.Tick(0.1f);
        reserve = 6;
        harness.Replicator.Tick(0.1f);

        harness.Network.Verify(n => n.SendAll(It.Is<IMessage>(m =>
            m is NetworkShipCondition && ((NetworkShipCondition)m).SpawnableReserve == 7)), Times.Once);
        harness.Network.Verify(n => n.SendAll(It.Is<IMessage>(m =>
            m is NetworkShipCondition && ((NetworkShipCondition)m).SpawnableReserve == 6
            && ((NetworkShipCondition)m).Revision == 2)), Times.Once);
        harness.Network.Verify(n => n.SendAll(It.Is<IMessage>(m => m is NetworkBattleShipSample)), Times.Exactly(3));
    }

    // R42: a frame longer than the interval carries its overshoot, so 35 ms frames still send at 20 Hz on average.
    [Theory]
    [InlineData(0.07f, 0.02f)]
    [InlineData(0.05f, 0f)]
    [InlineData(1f, BattleShipReplicator.SendIntervalSeconds)]
    public void CarryAfterSend_KeepsTheOvershootUpToOneInterval(float elapsed, float expected)
    {
        Assert.Equal(expected, BattleShipReplicator.CarryAfterSend(elapsed), 4);
    }

    // A long frame is followed by one catch-up sample, not a burst of the samples it missed.
    [Fact]
    public void Tick_AfterALongFrame_SendsOneCatchUpSampleOnly()
    {
        var harness = new Harness(committed: true);

        harness.Replicator.Tick(1f);
        harness.Replicator.Tick(0.001f);
        harness.Replicator.Tick(0.001f);
        harness.Replicator.Tick(0.001f);

        harness.Network.Verify(n => n.SendAll(It.Is<IMessage>(m =>
            m is NetworkBattleShipSample && ((NetworkBattleShipSample)m).OwnerControllerId == Own)), Times.Exactly(2));
    }

    // M9: vanilla latches a depleted side the first frame it reads one; the report rides the condition, so no frame length
    // expires it.
    [Fact]
    public void ReportedReserveOf_KeepsTheOwnersLatestReport_PastTheSampleLifetime()
    {
        var harness = new Harness(committed: false);
        var (copy, ship) = RegisterPeerCopy(harness, isNpcParty: false);
        Assert.Equal(0, harness.Replicator.ReportedReserveOf(copy));

        harness.Replicator.AcceptCondition(PeerCondition(ship, 1, reserve: 12));
        harness.Replicator.AcceptCondition(PeerCondition(ship, 2, reserve: 9));
        harness.Replicator.Tick(BattleShipReplicator.SampleLifetimeSeconds + 1.5f);

        Assert.Equal(9, harness.Replicator.ReportedReserveOf(copy));
        Assert.Equal(0, harness.Replicator.ReportedReserveOf(harness.OwnHull));
    }

    // A change of the reserve alone leaves the copy's hull state as it is.
    [Fact]
    public void AcceptCondition_AReserveOnlyChange_DoesNotApplyTheHullStateAgain()
    {
        var harness = new Harness(committed: false);
        var (copy, ship) = RegisterPeerCopy(harness, isNpcParty: false);

        harness.Replicator.AcceptCondition(PeerCondition(ship, 1, reserve: 12));
        harness.Replicator.AcceptCondition(PeerCondition(ship, 2, reserve: 11));

        harness.Engine.Verify(e => e.ApplyCondition(copy, It.IsAny<BattleShipCondition>()), Times.Once);
        Assert.Equal(11, harness.Replicator.ReportedReserveOf(copy));
    }

    // M9: the end tally counts a copy's reserve as its owner reports it and every other hull's from the engine.
    [Fact]
    public void HullReserve_ReadsAnotherOwnersHullFromItsReport_AndEveryOtherHullFromTheEngine()
    {
        var harness = new Harness(committed: false);
        var (copy, ship) = RegisterPeerCopy(harness, isNpcParty: false);
        var unregistered = CreateHull();
        harness.Engine.Setup(e => e.ReadSpawnableReserve(harness.OwnHull)).Returns(7);
        harness.Engine.Setup(e => e.ReadSpawnableReserve(unregistered)).Returns(4);
        harness.Engine.Setup(e => e.ReadSpawnableReserve(copy)).Returns(5);
        harness.Replicator.AcceptCondition(PeerCondition(ship, 1, reserve: 12));

        Assert.Equal(7, harness.Replicator.HullReserve(harness.OwnHull));
        Assert.Equal(12, harness.Replicator.HullReserve(copy));
        Assert.Equal(4, harness.Replicator.HullReserve(unregistered));
    }

    [Fact]
    public void ReportedReserveOf_DropsTheReport_WhenItsOwnerLeaves()
    {
        var harness = new Harness(committed: false);
        var (copy, ship) = RegisterPeerCopy(harness, isNpcParty: false);
        harness.Replicator.AcceptCondition(PeerCondition(ship, 1, reserve: 12));

        harness.Replicator.ForgetReportedReserveOf("someone_else");
        int afterOtherLeft = harness.Replicator.ReportedReserveOf(copy);
        harness.Replicator.ForgetReportedReserveOf(Peer);

        Assert.Equal(12, afterOtherLeft);
        Assert.Equal(0, harness.Replicator.ReportedReserveOf(copy));
    }

    // M12 r1, R7: the departure comes over the campaign connection, so a condition the owner sent before leaving can arrive
    // after it. The membership gate drops it until the owner enters again.
    [Fact]
    public void ReportedReserveOf_IgnoresALateConditionOfADepartedOwner_UntilItEntersAgain()
    {
        var harness = new Harness(committed: false);
        var (copy, ship) = RegisterPeerCopy(harness, isNpcParty: false);
        harness.Replicator.AcceptCondition(PeerCondition(ship, 1, reserve: 12));

        harness.Members.Remove(Peer);
        harness.Broker.Publish(this, new MissionPeerLeft(Peer, Instance));
        GameThread.Run(() => { }, blocking: true);
        harness.Replicator.AcceptCondition(PeerCondition(ship, 2, reserve: 9));
        int afterLateCondition = harness.Replicator.ReportedReserveOf(copy);
        var lateReject = (string)Inspected(harness, ship.ShipId)["lastReject"]!;
        harness.Members.Add(Peer);
        harness.Replicator.AcceptCondition(PeerCondition(ship, 3, reserve: 4));

        Assert.Equal(0, afterLateCondition);
        Assert.Equal("condition_not_in_mission", lateReject);
        Assert.Equal(4, harness.Replicator.ReportedReserveOf(copy));
    }

    // R7: the same gate drops a departed owner's late sample; its hull keeps the frame it had.
    [Fact]
    public void AcceptSample_FromAnOwnerNoLongerInTheMission_IsDropped()
    {
        var harness = new Harness(committed: false);
        var (_, ship) = RegisterPeerCopy(harness, isNpcParty: false);

        harness.Members.Remove(Peer);
        harness.Replicator.AcceptSample(new NetworkBattleShipSample(ship.ShipId, Peer, 1,
            NetworkBattleShipSample.FromFrame(MatrixFrame.Identity)));

        var inspected = Inspected(harness, ship.ShipId);
        Assert.Equal(0, (long)inspected["accepted"]!);
        Assert.Equal("not_in_mission", (string)inspected["lastReject"]!);
    }

    // R41a-2 E: a member's catch-up record is relayed past the server's join hold, so it can reach a joiner before the joiner
    // knows its owner is a member. It is held, not dropped, and spawns once the owner is a member.
    [Fact]
    public void AcceptSpawnRecords_FromAPlayerNotYetAMember_HoldsItAndSpawnsItOnMembership()
    {
        using var mission = new MissionCurrentScope();
        var harness = new Harness(committed: false);
        harness.SetupForeignSpawn();
        var record = ForeignRecord(Guid.NewGuid());

        harness.Members.Remove(Peer);
        harness.Replicator.AcceptSpawnRecords(new[] { record });
        harness.Replicator.Tick(0.1f);
        var stateWhileHeld = harness.Registry.StateOf(record.ShipId);
        harness.Members.Add(Peer);
        harness.Replicator.Tick(0.1f);

        Assert.Equal(ShipLifecycle.Awaiting, stateWhileHeld);
        Assert.True(harness.Registry.TryGet(record.ShipId, out var ship));
        Assert.Equal(Peer, ship.CurrentAuthority);
        harness.Engine.Verify(e => e.SpawnForeignHull(It.IsAny<BattleShipSpawnData>(), It.IsAny<Team>(), out It.Ref<Formation>.IsAny),
            Times.Once);
    }

    // A record held for its owner's membership goes with the owner when it leaves instead.
    [Fact]
    public void AcceptSpawnRecords_HeldForMembership_IsPrunedWhenItsOwnerLeaves()
    {
        using var mission = new MissionCurrentScope();
        var harness = new Harness(committed: true);
        harness.SetupForeignSpawn();
        var record = ForeignRecord(Guid.NewGuid());
        harness.Members.Remove(Peer);
        harness.Replicator.AcceptSpawnRecords(new[] { record });

        harness.Broker.Publish(this, new MissionPeerLeft(Peer, Instance));
        GameThread.Run(() => { }, blocking: true);
        harness.Members.Add(Peer);
        harness.Replicator.Tick(0.1f);

        Assert.Equal(ShipLifecycle.Removed, harness.Registry.StateOf(record.ShipId));
        Assert.Equal(0, (int)JObject.FromObject(harness.Replicator.Inspect())["pendingForeignHulls"]!);
        harness.Engine.Verify(e => e.SpawnForeignHull(It.IsAny<BattleShipSpawnData>(), It.IsAny<Team>(), out It.Ref<Formation>.IsAny),
            Times.Never);
    }

    // R7: a record a departed player sent can arrive after its leave; it is dropped, not held, until the player enters again.
    [Fact]
    public void AcceptSpawnRecords_FromADepartedPlayer_IsDroppedUntilItEntersAgain()
    {
        using var mission = new MissionCurrentScope();
        var harness = new Harness(committed: true);
        harness.SetupForeignSpawn();
        var late = ForeignRecord(Guid.NewGuid());
        var fresh = ForeignRecord(Guid.NewGuid());

        harness.Members.Remove(Peer);
        harness.Broker.Publish(this, new MissionPeerLeft(Peer, Instance));
        GameThread.Run(() => { }, blocking: true);
        harness.Replicator.AcceptSpawnRecords(new[] { late });
        int heldAfterLeave = (int)JObject.FromObject(harness.Replicator.Inspect())["pendingForeignHulls"]!;
        harness.Members.Add(Peer);
        harness.Broker.Publish(this, new NetworkMissionPeerEntered(Peer, Instance));
        GameThread.Run(() => { }, blocking: true);
        harness.Replicator.AcceptSpawnRecords(new[] { fresh });

        Assert.Equal(0, heldAfterLeave);
        Assert.False(harness.Registry.TryGet(late.ShipId, out _));
        Assert.True(harness.Registry.TryGet(fresh.ShipId, out _));
    }

    // D4: the server sends a naval disconnect as a leave, so a record the player sent before it dropped spawns nothing here
    // either, once that leave arrived.
    [Fact]
    public void AcceptSpawnRecords_FromAPlayerWhoseLeaveArrived_SpawnsNothing()
    {
        using var mission = new MissionCurrentScope();
        var harness = new Harness(committed: true);
        harness.SetupForeignSpawn();
        var record = ForeignRecord(Guid.NewGuid());

        harness.Members.Remove(Peer);
        harness.Broker.Publish(this, new MissionPeerLeft(Peer, Instance));
        GameThread.Run(() => { }, blocking: true);
        harness.Replicator.AcceptSpawnRecords(new[] { record });

        Assert.False(harness.Registry.TryGet(record.ShipId, out _));
        harness.Engine.Verify(e => e.SpawnForeignHull(It.IsAny<BattleShipSpawnData>(), It.IsAny<Team>(), out It.Ref<Formation>.IsAny),
            Times.Never);
    }

    [Fact]
    public void ReportedReserveOf_DropsTheReport_OnceTheHullSank()
    {
        var harness = new Harness(committed: false);
        var (copy, ship) = RegisterPeerCopy(harness, isNpcParty: false);
        harness.Replicator.AcceptCondition(PeerCondition(ship, 1, reserve: 12));

        harness.Replicator.AcceptCondition(new NetworkShipCondition(ship.ShipId, Peer, 2, 0,
            new BattleShipCondition(0f, 0f, 0f, new float[6], BattleShipCondition.Sunk), 12));

        Assert.Equal(0, harness.Replicator.ReportedReserveOf(copy));
    }

    [Fact]
    public void ReportedReserveOf_DropsAnAiHullsReport_WhenItsAuthorityMoves()
    {
        var harness = new Harness(committed: false);
        var (copy, ship) = RegisterPeerCopy(harness, isNpcParty: true);
        harness.Replicator.AcceptCondition(new NetworkShipCondition(ship.ShipId, Peer, 1, HostEpoch, Condition(100f), 12));
        int beforeMove = harness.Replicator.ReportedReserveOf(copy);

        harness.Replicator.TransferHostHulls("third_peer");

        Assert.Equal(12, beforeMove);
        Assert.Equal(0, harness.Replicator.ReportedReserveOf(copy));
    }

    // R48 re-review: the successor's first report for an AI hull can come over the mesh before the server's host change; it is
    // kept and applied when the change moves the hull to it, so this client doesn't read the side's reserve as 0.
    [Fact]
    public void SuccessorsReport_ThatOvertookTheHostChange_AppliesOnceTheHullMovesToIt()
    {
        var harness = new Harness(committed: false);
        harness.Members.Add("third_peer");
        var (copy, ship) = RegisterPeerCopy(harness, isNpcParty: true);
        harness.Replicator.AcceptCondition(new NetworkShipCondition(ship.ShipId, Peer, 1, HostEpoch, Condition(100f), 12));

        harness.Replicator.AcceptCondition(new NetworkShipCondition(ship.ShipId, "third_peer", 1, HostEpoch + 1, Condition(100f), 7));
        int beforeMove = harness.Replicator.ReportedReserveOf(copy);
        harness.Replicator.TransferHostHulls("third_peer");

        Assert.Equal(12, beforeMove);
        Assert.Equal(7, harness.Replicator.ReportedReserveOf(copy));
    }

    // R48 round 5 review: a host change lands in the session at once but moves the hulls from the queue; records arriving in
    // between don't take its epoch as the join epoch, so the successor's report at it is still kept and applied with the move.
    [Fact]
    public void JoinEpoch_IsNotTakenWhileAHostChangeIsQueued_SoTheSuccessorsReportApplies()
    {
        var harness = new Harness(committed: false, startEpoch: 0, queuedArrivals: true);
        harness.Members.Add("third_peer");
        var (copy, ship) = RegisterPeerCopy(harness, isNpcParty: true);

        harness.SessionEpoch = 2;
        harness.Broker.Publish(this, new BattleHostMigrated(Instance, Peer, "third_peer", 2));
        GameThread.Run(() => { }, blocking: true);
        harness.Replicator.AcceptSpawnRecords(Array.Empty<BattleShipSpawnData>());
        harness.Replicator.AcceptCondition(new NetworkShipCondition(ship.ShipId, "third_peer", 1, 2, Condition(100f), 7));
        int beforeMove = harness.Replicator.ReportedReserveOf(copy);
        harness.Arrivals.ApplyAll();

        Assert.Equal(0, beforeMove);
        Assert.Equal("third_peer", ship.CurrentAuthority);
        Assert.Equal(7, harness.Replicator.ReportedReserveOf(copy));
    }

    // R48 round 4 review: the epoch this client joined at counts as applied, so a report of that epoch from a controller that
    // isn't the hull's authority (a late joiner's view of a stale report, a returning old host) is dropped, not kept for later.
    [Fact]
    public void NotAuthorityReport_AtTheJoinEpoch_IsNotKeptForAHostChange()
    {
        var harness = new Harness(committed: false);
        harness.Members.Add("third_peer");
        var (copy, ship) = RegisterPeerCopy(harness, isNpcParty: true);

        harness.Replicator.AcceptCondition(new NetworkShipCondition(ship.ShipId, "third_peer", 1, HostEpoch, Condition(100f), 7));
        harness.Replicator.TransferHostHulls("third_peer");

        Assert.Equal(0, harness.Replicator.ReportedReserveOf(copy));
    }

    [Theory]
    [InlineData(true, "not_authority", 4, 3, true)]
    [InlineData(true, "not_authority", 3, 3, false)]
    [InlineData(false, "not_authority", 4, 3, false)]
    [InlineData(true, "stale", 4, 3, false)]
    public void IsEarlyHostCondition_OnlyAnAiHullsReportFromANewerHostEpoch(bool followsHost, string rejection, int conditionEpoch,
        int appliedEpoch, bool expected)
    {
        Assert.Equal(expected, BattleShipReplicator.IsEarlyHostCondition(followsHost, rejection, conditionEpoch, appliedEpoch));
    }

    // A new owner of an AI hull reports its own reserve at its first check, even when it equals the last one reported.
    [Fact]
    public void TransferHostHulls_ToThisClient_SendsTheReserveAgainAtTheFirstCheck()
    {
        var harness = new Harness(committed: true);
        var (_, ship) = RegisterPeerCopy(harness, isNpcParty: true);
        harness.Replicator.AcceptCondition(new NetworkShipCondition(ship.ShipId, Peer, 1, HostEpoch, Condition(100f), 12));
        harness.Engine.Setup(e => e.ReadCondition(It.IsAny<MissionObject>())).Returns(Condition(100f));
        harness.Engine.Setup(e => e.ReadSpawnableReserve(It.IsAny<MissionObject>())).Returns(12);

        harness.Replicator.TransferHostHulls(Own);
        harness.Replicator.Tick(0.1f);

        harness.Network.Verify(n => n.SendAll(It.Is<IMessage>(m =>
            m is NetworkShipCondition && ((NetworkShipCondition)m).ShipId == ship.ShipId
            && ((NetworkShipCondition)m).SpawnableReserve == 12)), Times.Once);
    }

    private static NetworkShipCondition PeerCondition(NetworkShipInfo ship, long revision, int reserve) =>
        new NetworkShipCondition(ship.ShipId, Peer, revision, 0, Condition(100f), reserve);

    // M8a: an owner's removal frees the copy's seats and stations, parks whoever is still aboard a tick later and removes the
    // hull the tick after (R33), and the ship id stays Removed, so a late sample is dropped and a late record never spawns it.
    [Fact]
    public void AcceptRemoval_ReleasesSeatsThenParksTheDeckBeforeRemovingTheCopy_AndTheShipNeverComesBack()
    {
        using var mission = new MissionCurrentScope();
        var harness = new Harness(committed: false);
        harness.SetupForeignSpawn();
        var (copy, ship) = RegisterPeerCopy(harness, isNpcParty: false);
        var order = new List<string>();
        harness.Stations.Setup(s => s.ReleaseSeatsOn(copy)).Callback(() => order.Add("seats")).Returns(0);
        harness.Engine.Setup(e => e.ReleaseHullStations(copy)).Callback(() => order.Add("stations"));
        harness.Engine.Setup(e => e.ParkHullAgents(copy)).Callback(() => order.Add("park")).Returns(Array.Empty<HullSwapAgent>());
        harness.Engine.Setup(e => e.RemoveHull(copy)).Callback(() => order.Add("remove"));
        harness.Engine.Setup(e => e.SideOf(copy)).Returns(BattleSideEnum.Attacker);

        harness.Replicator.AcceptRemoval(new NetworkShipRemoved(ship.ShipId, Peer, ShipRemovalReason.Retreat, missionEnding: true));
        harness.Replicator.AcceptSample(new NetworkBattleShipSample(ship.ShipId, Peer, 1,
            NetworkBattleShipSample.FromFrame(MatrixFrame.Identity)));
        harness.Replicator.AcceptSpawnRecords(new[] { ForeignRecord(ship.ShipId) });
        harness.Replicator.Tick(0.1f);
        var afterFirstTick = order.ToArray();
        harness.Replicator.Tick(0.1f);

        Assert.Equal(new[] { "seats", "stations", "park" }, afterFirstTick);
        Assert.Equal(new[] { "seats", "stations", "park", "remove" }, order);
        Assert.Equal(ShipLifecycle.Removed, harness.Registry.StateOf(ship.ShipId));
        Assert.Equal(1, harness.Replicator.DepartedHulls(BattleSideEnum.Attacker));
        Assert.Equal(0, harness.Replicator.DepartedHulls(BattleSideEnum.Defender));
        harness.Engine.Verify(e => e.SpawnForeignHull(It.IsAny<BattleShipSpawnData>(), It.IsAny<Team>(), out It.Ref<Formation>.IsAny),
            Times.Never);
        var inspected = JObject.FromObject(harness.Replicator.Inspect());
        Assert.Equal(1, (long)inspected["removalsAccepted"]!);
        Assert.Equal(0, (int)inspected["pendingForeignHulls"]!);
    }

    [Fact]
    public void AcceptRemoval_FromAnotherThanTheHullsAuthority_KeepsTheHull()
    {
        var harness = new Harness(committed: false);
        var (copy, ship) = RegisterPeerCopy(harness, isNpcParty: false);

        harness.Replicator.AcceptRemoval(new NetworkShipRemoved(ship.ShipId, "someone_else", ShipRemovalReason.Leave, missionEnding: true));
        harness.Replicator.AcceptRemoval(new NetworkShipRemoved(harness.OwnShipId, Own, ShipRemovalReason.Leave, missionEnding: true));

        Assert.Equal(ShipLifecycle.Registered, harness.Registry.StateOf(ship.ShipId));
        Assert.Equal(ShipLifecycle.Registered, harness.Registry.StateOf(harness.OwnShipId));
        harness.Engine.Verify(e => e.RemoveHull(It.IsAny<MissionObject>()), Times.Never);
    }

    // M8a: the departing hull's crew leaves with it; an ally on its deck goes back to the hull it crews, and an agent with
    // no hull of its own goes to the nearest friendly one.
    [Fact]
    public void AcceptRemoval_MovesOtherOwnersAgentsOffTheDeck_AndLeavesTheHullsOwnCrew()
    {
        var harness = new Harness(committed: false);
        var (copy, ship) = RegisterPeerCopy(harness, isNpcParty: false);
        var ally = CreateAgent();
        var crew = CreateAgent();
        var stray = CreateAgent();
        var nearest = CreateHull();
        harness.Engine.Setup(e => e.AgentsOnDeck(copy)).Returns(new[] { ally, crew, stray });
        harness.Engine.Setup(e => e.CrewHullOf(ally)).Returns(harness.OwnHull);
        harness.Engine.Setup(e => e.CrewHullOf(crew)).Returns(copy);
        harness.Engine.Setup(e => e.NearestFriendlyHull(stray, It.IsAny<IReadOnlyCollection<MissionObject>>())).Returns(nearest);

        harness.Replicator.AcceptRemoval(new NetworkShipRemoved(ship.ShipId, Peer, ShipRemovalReason.Retreat, missionEnding: true));

        harness.Engine.Verify(e => e.MoveAgentToHull(ally, harness.OwnHull), Times.Once);
        harness.Engine.Verify(e => e.MoveAgentToHull(stray, nearest), Times.Once);
        harness.Engine.Verify(e => e.MoveAgentToHull(crew, It.IsAny<MissionObject>()), Times.Never);
        Assert.Equal(2, (long)JObject.FromObject(harness.Replicator.Inspect())["agentsMovedOffDepartedHulls"]!);
    }

    // M8c r1: a hull already queued for removal goes with whoever stands on it then, so an agent on a later departing deck
    // whose crew hull is queued goes to the nearest friendly hull, and that one is never a queued hull either.
    [Fact]
    public void AcceptRemoval_MovesNobodyOntoAHullAlreadyQueuedForRemoval()
    {
        var harness = new Harness(committed: false);
        var (first, firstShip) = RegisterPeerCopy(harness, isNpcParty: false);
        var (second, secondShip) = RegisterPeerCopy(harness, isNpcParty: false);
        var ally = CreateAgent();
        harness.Engine.Setup(e => e.AgentsOnDeck(second)).Returns(new[] { ally });
        harness.Engine.Setup(e => e.CrewHullOf(ally)).Returns(first);
        IReadOnlyCollection<MissionObject>? excluded = null;
        harness.Engine.Setup(e => e.NearestFriendlyHull(ally, It.IsAny<IReadOnlyCollection<MissionObject>>()))
            .Callback<Agent, IReadOnlyCollection<MissionObject>>((_, hulls) => excluded = hulls)
            .Returns(harness.OwnHull);

        harness.Replicator.AcceptRemoval(new NetworkShipRemoved(firstShip.ShipId, Peer, ShipRemovalReason.Retreat, missionEnding: false));
        harness.Replicator.AcceptRemoval(new NetworkShipRemoved(secondShip.ShipId, Peer, ShipRemovalReason.Leave, missionEnding: true));

        harness.Engine.Verify(e => e.MoveAgentToHull(ally, first), Times.Never);
        harness.Engine.Verify(e => e.MoveAgentToHull(ally, harness.OwnHull), Times.Once);
        Assert.Contains(first, excluded!);
        Assert.Contains(second, excluded!);
    }

    // R41a fix-loop C3: a host and a player drop together. The successor is swapping an AI hull when the player's hull leaves,
    // so an AI boarder on the player's deck goes to another friendly hull, not onto the hull the swap replaces next tick.
    [Fact]
    public void AcceptRemoval_MovesNobodyOntoAHullMidSwap()
    {
        var harness = new Harness(committed: true);
        var aiHull = CreateHull();
        var ai = new NetworkShipInfo(Guid.NewGuid(), Peer, "MapEventParty_9", true, aiHull, null);
        harness.Registry.TryRegister(ai);
        harness.LiveHulls.Add(aiHull);
        harness.Engine.Setup(e => e.ParkHullAgents(aiHull)).Returns(Array.Empty<HullSwapAgent>());
        var (playerCopy, player) = RegisterPeerCopy(harness, isNpcParty: false);
        var boarder = CreateAgent();
        harness.Engine.Setup(e => e.AgentsOnDeck(playerCopy)).Returns(new[] { boarder });
        harness.Engine.Setup(e => e.CrewHullOf(boarder)).Returns(aiHull);
        IReadOnlyCollection<MissionObject>? excluded = null;
        harness.Engine.Setup(e => e.NearestFriendlyHull(boarder, It.IsAny<IReadOnlyCollection<MissionObject>>()))
            .Callback<Agent, IReadOnlyCollection<MissionObject>>((_, hulls) => excluded = hulls)
            .Returns(harness.OwnHull);
        harness.Replicator.TransferHostHulls(Own);
        harness.Replicator.Tick(0.1f);

        harness.Replicator.AcceptRemoval(new NetworkShipRemoved(player.ShipId, Peer, ShipRemovalReason.Leave, missionEnding: true));

        harness.Engine.Verify(e => e.MoveAgentToHull(boarder, aiHull), Times.Never);
        harness.Engine.Verify(e => e.MoveAgentToHull(boarder, harness.OwnHull), Times.Once);
        Assert.Contains(aiHull, excluded!);
    }

    // M8a: a player who left takes its hulls and its records still waiting here; AI hulls it held move with the host instead.
    [Fact]
    public void RemoveHullsOf_RemovesTheDepartedPlayersHullsAndWaitingRecords_AndKeepsTheAiHullsItHeld()
    {
        using var mission = new MissionCurrentScope();
        var harness = new Harness(committed: false);
        bool deploying = true;
        harness.Engine.SetupGet(e => e.IsDeploymentMode).Returns(() => deploying);
        harness.SetupForeignSpawn();
        var (playerCopy, player) = RegisterPeerCopy(harness, isNpcParty: false);
        var (aiCopy, ai) = RegisterPeerCopy(harness, isNpcParty: true);
        var waiting = ForeignRecord(Guid.NewGuid());
        harness.Replicator.AcceptSpawnRecords(new[] { waiting });
        bool waitingBefore = harness.Registry.StateOf(waiting.ShipId) == ShipLifecycle.Awaiting;

        harness.Replicator.RemoveHullsOf(Peer);
        deploying = false;
        harness.Replicator.Tick(0.1f);
        harness.Replicator.Tick(0.1f);

        Assert.True(waitingBefore);
        Assert.Equal(ShipLifecycle.Removed, harness.Registry.StateOf(player.ShipId));
        Assert.Equal(ShipLifecycle.Removed, harness.Registry.StateOf(waiting.ShipId));
        Assert.Equal(ShipLifecycle.Registered, harness.Registry.StateOf(ai.ShipId));
        harness.Engine.Verify(e => e.RemoveHull(playerCopy), Times.Once);
        harness.Engine.Verify(e => e.RemoveHull(aiCopy), Times.Never);
        harness.Engine.Verify(e => e.SpawnForeignHull(It.IsAny<BattleShipSpawnData>(), It.IsAny<Team>(), out It.Ref<Formation>.IsAny),
            Times.Never);
    }

    // An AI hull follows the battle host, so its record still waiting here when the host departs is kept and spawns under the
    // next host.
    [Fact]
    public void RemoveHullsOf_KeepsAWaitingAiRecordOfTheDepartingHost_AndSpawnsItUnderTheNextHost()
    {
        using var mission = new MissionCurrentScope();
        var harness = new Harness(committed: true);
        bool deploying = true;
        harness.Engine.SetupGet(e => e.IsDeploymentMode).Returns(() => deploying);
        harness.SetupForeignSpawn();
        var aiRecord = new BattleShipSpawnData(Guid.NewGuid(), Peer, "MapEventParty_2", true, BattleSideEnum.Defender, 0,
            NetworkBattleShipSample.FromFrame(MatrixFrame.Identity), "northern_light_ship", null, null, null, null,
            100f, 50f, 1, "", originalOwnerControllerId: "first_host");
        harness.Replicator.AcceptSpawnRecords(new[] { aiRecord });

        harness.Replicator.RemoveHullsOf(Peer);
        harness.Members.Remove(Peer);
        harness.Broker.Publish(this, new BattleHostMigrated(Instance, Peer, Own));
        GameThread.Run(() => { }, blocking: true);
        deploying = false;
        harness.Replicator.Tick(0.1f);
        harness.Replicator.Tick(0.1f);

        Assert.True(aiRecord.FollowsBattleHost);
        Assert.True(harness.Registry.TryGet(aiRecord.ShipId, out var ship));
        Assert.Equal(Own, ship.CurrentAuthority);
        Assert.Equal("first_host", ship.OriginalOwner);
    }

    // M8a: the owner tells peers once its announced hull leaves; a swap, a hull nobody knows of yet and an AI hull at the
    // mission's end (it moves to the next host) send nothing.
    [Fact]
    public void AnnounceRemoval_SendsOnlyForAnAnnouncedOwnHull_AndNotForAnAiHullAtMissionEnd()
    {
        var harness = new Harness(committed: true);
        var aiHull = CreateHull();
        harness.Registry.TryRegister(new NetworkShipInfo(Guid.NewGuid(), Own, "MapEventParty_9", true, aiHull, null));
        harness.LiveHulls.Add(aiHull);
        harness.Replicator.AnnounceRemoval(harness.OwnHull, ShipRemovalReason.Retreat, missionEnding: false);
        harness.Network.Verify(n => n.SendAll(It.Is<IMessage>(m => m is NetworkShipRemoved)), Times.Never);
        harness.Replicator.Tick(0.1f);

        harness.Replicator.AnnounceRemoval(aiHull, ShipRemovalReason.Leave, missionEnding: true);
        harness.Replicator.AnnounceRemoval(harness.OwnHull, ShipRemovalReason.Retreat, missionEnding: true);

        harness.Network.Verify(n => n.SendAll(It.Is<IMessage>(m => m is NetworkShipRemoved)), Times.Once);
        harness.Network.Verify(n => n.SendAll(It.Is<IMessage>(m => m is NetworkShipRemoved
            && ((NetworkShipRemoved)m).ShipId == harness.OwnShipId
            && ((NetworkShipRemoved)m).Reason == ShipRemovalReason.Retreat)), Times.Once);
    }

    [Fact]
    public void AnnounceRemoval_MidMission_ClearsTheDeckHere_AndSendsTheHullsLastRopesFirst()
    {
        var harness = new Harness(committed: true);
        harness.Replicator.Tick(0.1f);
        var ropes = new[] { new BattleRopeState { SourceKey = "0:rope", Generation = 1 } };
        harness.Engine.Setup(e => e.CaptureRopes(harness.OwnHull, It.IsAny<Func<MissionObject, Guid>>())).Returns(ropes);
        harness.Engine.Setup(e => e.SideOf(harness.OwnHull)).Returns(BattleSideEnum.Defender);
        var sent = new List<IMessage>();
        harness.Network.Setup(n => n.SendAll(It.IsAny<IMessage>())).Callback<IMessage>(sent.Add);

        harness.Replicator.AnnounceRemoval(harness.OwnHull, ShipRemovalReason.Retreat, missionEnding: false);

        harness.Stations.Verify(s => s.ReleaseSeatsOn(harness.OwnHull), Times.Once);
        Assert.IsType<NetworkBattleRopeFinal>(sent[0]);
        Assert.IsType<NetworkShipRemoved>(sent[1]);
        Assert.Equal(1, harness.Replicator.DepartedHulls(BattleSideEnum.Defender));
    }

    // M8a r1: a hull's reserve rides its on-change condition, so a peer entering after the commit gets the last one with the
    // hull's record, otherwise it would count no reserve for the hull if it became battle host.
    [Fact]
    public void PeerEntered_AfterTheCommit_SendsTheEntrantTheOwnHullsRecordThenItsLastConditionAndReserve()
    {
        var harness = new Harness(committed: true);
        harness.Engine.Setup(e => e.ReadCondition(harness.OwnHull)).Returns(Condition(100f));
        harness.Engine.Setup(e => e.ReadSpawnableReserve(harness.OwnHull)).Returns(7);
        harness.Replicator.Tick(0.1f);
        harness.Replicator.Tick(0.1f);
        var sent = new List<IMessage>();
        harness.Network.Setup(n => n.Send("joiner", It.IsAny<IMessage>())).Callback<string, IMessage>((_, message) => sent.Add(message));

        harness.Broker.Publish(this, new NetworkMissionPeerEntered("joiner", Instance));
        GameThread.Run(() => { }, blocking: true);

        Assert.Equal(2, sent.Count);
        Assert.IsType<NetworkSpawnBattleShips>(sent[0]);
        var condition = Assert.IsType<NetworkShipCondition>(sent[1]);
        Assert.Equal(harness.OwnShipId, condition.ShipId);
        Assert.Equal(1, condition.Revision);
        Assert.Equal(7, condition.SpawnableReserve);
    }

    private static Agent CreateAgent() => (Agent)RuntimeHelpers.GetUninitializedObject(typeof(Agent));

    private static (MissionObject Copy, NetworkShipInfo Ship) RegisterPeerCopy(Harness harness, bool isNpcParty)
    {
        var copy = CreateHull();
        var ship = new NetworkShipInfo(Guid.NewGuid(), Peer, "MapEventParty_2", isNpcParty, copy, null);
        harness.Registry.TryRegister(ship);
        harness.LiveHulls.Add(copy);
        return (copy, ship);
    }

    [Fact]
    public void Removal_KeepsWhetherTheOwnersMissionEndedOverTheWire()
    {
        var shipId = Guid.NewGuid();

        var ending = Serializer.DeepClone(new NetworkShipRemoved(shipId, Peer, ShipRemovalReason.Leave, missionEnding: true));
        var staying = Serializer.DeepClone(new NetworkShipRemoved(shipId, Peer, ShipRemovalReason.Retreat, missionEnding: false));

        Assert.True(ending.MissionEnding);
        Assert.Equal(ShipRemovalReason.Leave, ending.Reason);
        Assert.False(staying.MissionEnding);
        Assert.Equal(shipId, staying.ShipId);
    }

    [Fact]
    public void Condition_KeepsItsReserveOverTheWire_AndANegativeReserveIsRejected()
    {
        var ship = new NetworkShipInfo(Guid.NewGuid(), Peer, "MapEventParty_2", false, CreateHull(), null);

        var wire = Serializer.DeepClone(new NetworkShipCondition(ship.ShipId, Peer, 1, 0, Condition(100f), 14));

        Assert.Equal(14, wire.SpawnableReserve);
        Assert.Equal("invalid_reserve", BattleShipConditionStream.ValidateCondition(ship, Own,
            new NetworkShipCondition(ship.ShipId, Peer, 1, 0, Condition(100f), -1), 0, 0, InMission));
    }

    private static JToken Inspected(Harness harness, Guid shipId) =>
        JObject.FromObject(harness.Replicator.Inspect())["ships"]!.Single(entry => (Guid)entry["shipId"]! == shipId);

    [Theory]
    [InlineData(2, 10, 3, 4, "stale_epoch")]
    [InlineData(3, 3, 3, 4, "stale")]
    [InlineData(4, 1, 3, 10, null)]
    [InlineData(3, 11, 3, 10, null)]
    public void ValidateSample_AiHull_RestartsTheSequenceOnlyForANewerHostEpoch(
        int sampleEpoch, long sequence, int acceptedEpoch, long acceptedSequence, string expected)
    {
        var ship = new NetworkShipInfo(Guid.NewGuid(), Peer, "MapEventParty_9", true, CreateHull(), null);
        var sample = new NetworkBattleShipSample(ship.ShipId, Peer, sequence,
            NetworkBattleShipSample.FromFrame(MatrixFrame.Identity), default, null, sampleEpoch);

        Assert.Equal(expected, BattleShipReplicator.ValidateSample(ship, Own, sample, acceptedSequence, acceptedEpoch, InMission));
    }

    [Fact]
    public void ValidateSample_PlayerHull_IgnoresTheHostEpoch()
    {
        var ship = new NetworkShipInfo(Guid.NewGuid(), Peer, "MapEventParty_2", false, CreateHull(), null);
        var older = new NetworkBattleShipSample(ship.ShipId, Peer, 11,
            NetworkBattleShipSample.FromFrame(MatrixFrame.Identity), default, null, 1);
        var newerButStale = new NetworkBattleShipSample(ship.ShipId, Peer, 5,
            NetworkBattleShipSample.FromFrame(MatrixFrame.Identity), default, null, 9);

        Assert.Null(BattleShipReplicator.ValidateSample(ship, Own, older, 10, 5, InMission));
        Assert.Equal("stale", BattleShipReplicator.ValidateSample(ship, Own, newerButStale, 10, 5, InMission));
    }

    [Fact]
    public void Tick_StampsOwnSamplesWithTheHostEpoch()
    {
        var harness = new Harness(committed: true);

        harness.Replicator.Tick(0.1f);

        harness.Network.Verify(n => n.SendAll(It.Is<IMessage>(m =>
            m is NetworkBattleShipSample && ((NetworkBattleShipSample)m).HostEpoch == HostEpoch)), Times.Once);
    }

    [Fact]
    public void RegisterNpcHull_RegistersAHostOwnedAiHullAndAnnouncesItBeforeTheCommit()
    {
        var harness = new Harness(committed: false);
        var hull = CreateHull();

        var shipId = harness.Replicator.RegisterNpcHull(hull, null, "MapEventParty_9");

        Assert.True(harness.Registry.TryGet(shipId, out var ship));
        Assert.True(ship.IsNpcParty);
        Assert.Equal(Own, ship.CurrentAuthority);
        Assert.Equal("MapEventParty_9", ship.MapEventPartyId);
        harness.Network.Verify(n => n.SendAll(It.Is<IMessage>(m =>
            m is NetworkSpawnBattleShips && ((NetworkSpawnBattleShips)m).Ships.Single().ShipId == shipId)), Times.Once);
    }

    [Fact]
    public void TransferHostHulls_ToThisClient_TakesOverOnlyTheAiHulls()
    {
        var harness = new Harness(committed: true);
        var aiHull = CreateHull();
        var peerHull = CreateHull();
        var ai = new NetworkShipInfo(Guid.NewGuid(), Peer, "MapEventParty_9", true, aiHull, null);
        var peerShip = new NetworkShipInfo(Guid.NewGuid(), Peer, "MapEventParty_2", false, peerHull, null);
        harness.Registry.TryRegister(ai);
        harness.Registry.TryRegister(peerShip);
        harness.LiveHulls.Add(aiHull);
        harness.LiveHulls.Add(peerHull);

        Assert.Equal(1, harness.Replicator.TransferHostHulls(Own));
        Assert.Equal(0, harness.Replicator.TransferHostHulls(Own));
        harness.Replicator.Tick(0.1f);

        Assert.Equal(Own, ai.CurrentAuthority);
        Assert.Equal(Peer, peerShip.CurrentAuthority);
        harness.Engine.Verify(e => e.ParkHullAgents(aiHull), Times.Once);
        harness.Engine.Verify(e => e.ParkHullAgents(peerHull), Times.Never);
    }

    [Fact]
    public void TransferHostHulls_ToThisClient_ChangesNoHullBeforeTheNextMissionTick()
    {
        var harness = new Harness(committed: true);
        var copy = CreateHull();
        harness.Registry.TryRegister(new NetworkShipInfo(Guid.NewGuid(), Peer, "MapEventParty_9", true, copy, null));

        harness.Replicator.TransferHostHulls(Own);

        harness.Engine.Verify(e => e.ParkHullAgents(It.IsAny<MissionObject>()), Times.Never);
        harness.Engine.Verify(e => e.ReplaceHull(It.IsAny<MissionObject>()), Times.Never);
        harness.Engine.Verify(e => e.BoardHullAgents(It.IsAny<MissionObject>(), It.IsAny<IReadOnlyList<HullSwapAgent>>()), Times.Never);
    }

    [Fact]
    public void Tick_AfterTakeover_ParksThenReplacesThenBoardsOneStepPerTick()
    {
        var harness = new Harness(committed: true);
        var copy = CreateHull();
        var fresh = CreateHull();
        var ai = new NetworkShipInfo(Guid.NewGuid(), Peer, "MapEventParty_9", true, copy, null);
        harness.Registry.TryRegister(ai);
        harness.LiveHulls.Add(copy);
        var parked = new[] { new HullSwapAgent(null, isCrew: true, isParked: true, Vec3.Zero, Vec3.Zero) };
        harness.Engine.Setup(e => e.ParkHullAgents(copy)).Returns(parked);
        harness.Engine.Setup(e => e.ReplaceHull(copy)).Returns(fresh).Callback(() =>
        {
            harness.LiveHulls.Remove(copy);
            harness.LiveHulls.Add(fresh);
        });
        harness.Replicator.TransferHostHulls(Own);

        harness.Replicator.Tick(0.1f);
        harness.Engine.Verify(e => e.ParkHullAgents(copy), Times.Once);
        harness.Engine.Verify(e => e.ReplaceHull(It.IsAny<MissionObject>()), Times.Never);

        harness.Replicator.Tick(0.1f);
        harness.Engine.Verify(e => e.ReplaceHull(copy), Times.Once);
        harness.Engine.Verify(e => e.BoardHullAgents(It.IsAny<MissionObject>(), It.IsAny<IReadOnlyList<HullSwapAgent>>()), Times.Never);
        Assert.True(harness.Registry.TryGet(ai.ShipId, out var ship));
        Assert.Same(fresh, ship.Hull);
        Assert.False(harness.Registry.TryGetByHull(copy, out _));

        harness.Replicator.Tick(0.1f);
        harness.Replicator.Tick(0.1f);
        harness.Engine.Verify(e => e.BoardHullAgents(fresh, parked), Times.Once);
        harness.Engine.Verify(e => e.ParkHullAgents(It.IsAny<MissionObject>()), Times.Once);
        harness.Engine.Verify(e => e.ReplaceHull(It.IsAny<MissionObject>()), Times.Once);
        Assert.Equal(Own, ship.CurrentAuthority);
    }

    [Fact]
    public void Tick_WhenTheHullCannotBeReplaced_BoardsTheParkedAgentsBackOntoIt()
    {
        var harness = new Harness(committed: true);
        var copy = CreateHull();
        var ai = new NetworkShipInfo(Guid.NewGuid(), Peer, "MapEventParty_9", true, copy, null);
        harness.Registry.TryRegister(ai);
        harness.LiveHulls.Add(copy);
        var parked = new[] { new HullSwapAgent(null, isCrew: true, isParked: true, Vec3.Zero, Vec3.Zero) };
        harness.Engine.Setup(e => e.ParkHullAgents(copy)).Returns(parked);
        harness.Replicator.TransferHostHulls(Own);

        harness.Replicator.Tick(0.1f);
        harness.Replicator.Tick(0.1f);
        harness.Replicator.Tick(0.1f);

        harness.Engine.Verify(e => e.BoardHullAgents(copy, parked), Times.Once);
        Assert.Same(copy, ai.Hull);
    }

    [Fact]
    public void Tick_DuringShipDeployment_WaitsBeforeParkingTheAgents()
    {
        var harness = new Harness(committed: true);
        var copy = CreateHull();
        harness.Registry.TryRegister(new NetworkShipInfo(Guid.NewGuid(), Peer, "MapEventParty_9", true, copy, null));
        harness.LiveHulls.Add(copy);
        bool deploying = true;
        harness.Engine.SetupGet(e => e.IsDeploymentMode).Returns(() => deploying);
        harness.Replicator.TransferHostHulls(Own);

        harness.Replicator.Tick(0.1f);
        harness.Replicator.Tick(0.1f);
        harness.Engine.Verify(e => e.ParkHullAgents(It.IsAny<MissionObject>()), Times.Never);

        deploying = false;
        harness.Replicator.Tick(0.1f);
        harness.Engine.Verify(e => e.ParkHullAgents(copy), Times.Once);
        harness.Engine.Verify(e => e.ReplaceHull(It.IsAny<MissionObject>()), Times.Never);
    }

    [Fact]
    public void Tick_WhenReplacingTheHullThrows_HandsTheParkedAgentsBack()
    {
        var harness = new Harness(committed: true);
        var copy = CreateHull();
        harness.Registry.TryRegister(new NetworkShipInfo(Guid.NewGuid(), Peer, "MapEventParty_9", true, copy, null));
        harness.LiveHulls.Add(copy);
        var parked = new[] { new HullSwapAgent(null, isCrew: true, isParked: true, Vec3.Zero, Vec3.Zero) };
        harness.Engine.Setup(e => e.ParkHullAgents(copy)).Returns(parked);
        harness.Engine.Setup(e => e.ReplaceHull(copy)).Throws(new InvalidOperationException("spawn failed"));
        harness.Replicator.TransferHostHulls(Own);

        harness.Replicator.Tick(0.1f);
        harness.Replicator.Tick(0.1f);
        harness.Replicator.Tick(0.1f);

        harness.Engine.Verify(e => e.BoardHullAgents(copy, parked), Times.Once);
        harness.Engine.Verify(e => e.ReplaceHull(copy), Times.Once);
    }

    [Fact]
    public void Tick_WhenTheShipGoesAwayMidSwap_HandsTheParkedAgentsBackWithoutAHull()
    {
        var harness = new Harness(committed: true);
        var copy = CreateHull();
        harness.Registry.TryRegister(new NetworkShipInfo(Guid.NewGuid(), Peer, "MapEventParty_9", true, copy, null));
        harness.LiveHulls.Add(copy);
        var parked = new[] { new HullSwapAgent(null, isCrew: true, isParked: true, Vec3.Zero, Vec3.Zero) };
        harness.Engine.Setup(e => e.ParkHullAgents(copy)).Returns(parked);
        harness.Replicator.TransferHostHulls(Own);

        // The first tick parks the agents, then drops the hull that left the mission from the registry.
        harness.LiveHulls.Remove(copy);
        harness.Replicator.Tick(0.1f);
        harness.Replicator.Tick(0.1f);
        harness.Replicator.Tick(0.1f);

        harness.Engine.Verify(e => e.ParkHullAgents(copy), Times.Once);
        harness.Engine.Verify(e => e.BoardHullAgents(null, parked), Times.Once);
        harness.Engine.Verify(e => e.ReplaceHull(It.IsAny<MissionObject>()), Times.Never);
    }

    [Fact]
    public void TransferHostHulls_AwayMidSwap_BoardsTheParkedAgentsAndStopsTheSwap()
    {
        var harness = new Harness(committed: true);
        var copy = CreateHull();
        harness.Registry.TryRegister(new NetworkShipInfo(Guid.NewGuid(), Peer, "MapEventParty_9", true, copy, null));
        harness.LiveHulls.Add(copy);
        var parked = new[] { new HullSwapAgent(null, isCrew: true, isParked: true, Vec3.Zero, Vec3.Zero) };
        harness.Engine.Setup(e => e.ParkHullAgents(copy)).Returns(parked);
        harness.Replicator.TransferHostHulls(Own);
        harness.Replicator.Tick(0.1f);

        harness.Replicator.TransferHostHulls(Peer);
        harness.Replicator.Tick(0.1f);

        harness.Engine.Verify(e => e.BoardHullAgents(copy, parked), Times.Once);
        harness.Engine.Verify(e => e.ReleaseHull(copy), Times.Once);
        harness.Engine.Verify(e => e.ReplaceHull(It.IsAny<MissionObject>()), Times.Never);
    }

    [Fact]
    public void ReplaceNpcHull_QueuesOnlyAnAiHullThisClientSimulatesOnce()
    {
        var harness = new Harness(committed: true);
        var foreignAi = new NetworkShipInfo(Guid.NewGuid(), Peer, "MapEventParty_9", true, CreateHull(), null);
        var ownAi = new NetworkShipInfo(Guid.NewGuid(), Own, "MapEventParty_8", true, CreateHull(), null);
        harness.Registry.TryRegister(foreignAi);
        harness.Registry.TryRegister(ownAi);

        Assert.NotNull(harness.Replicator.ReplaceNpcHull(Guid.NewGuid()));
        Assert.NotNull(harness.Replicator.ReplaceNpcHull(harness.OwnShipId));
        Assert.NotNull(harness.Replicator.ReplaceNpcHull(foreignAi.ShipId));
        Assert.Null(harness.Replicator.ReplaceNpcHull(ownAi.ShipId));
        Assert.NotNull(harness.Replicator.ReplaceNpcHull(ownAi.ShipId));
    }

    [Fact]
    public void TransferHostHulls_AwayFromThisClient_HandsTheHullBackToSamples()
    {
        var harness = new Harness(committed: true);
        var aiHull = CreateHull();
        var ai = new NetworkShipInfo(Guid.NewGuid(), Own, "MapEventParty_9", true, aiHull, null);
        harness.Registry.TryRegister(ai);

        harness.Replicator.TransferHostHulls(Peer);

        Assert.Equal(Peer, ai.CurrentAuthority);
        harness.Engine.Verify(e => e.ReleaseHull(aiHull), Times.Once);
    }

    [Fact]
    public void TransferHostHulls_BetweenTwoPeers_KeepsTheLocalCopyACopy()
    {
        var harness = new Harness(committed: true);
        var aiHull = CreateHull();
        var ai = new NetworkShipInfo(Guid.NewGuid(), Peer, "MapEventParty_9", true, aiHull, null);
        harness.Registry.TryRegister(ai);
        harness.LiveHulls.Add(aiHull);

        harness.Replicator.TransferHostHulls("successor");
        harness.Replicator.Tick(0.1f);

        Assert.Equal("successor", ai.CurrentAuthority);
        harness.Engine.Verify(e => e.ReleaseHull(It.IsAny<MissionObject>()), Times.Never);
        harness.Engine.Verify(e => e.ParkHullAgents(It.IsAny<MissionObject>()), Times.Never);
    }

    // D4: a leave, which is also how a naval disconnect arrives, removes the player's hulls after parking their decks; the AI
    // hulls it simulated stay and move to the next host with the migration, while another player's hull stays with its owner.
    [Fact]
    public void MissionPeerLeft_RemovesThePlayersHulls_AndTheAiHullsItHeldMoveWithTheMigration()
    {
        using var mission = new MissionCurrentScope();
        var harness = new Harness(committed: true);
        var departedShip = RegisterForeign(harness, Peer, isNpcParty: false);
        var aiShip = RegisterForeign(harness, Peer, isNpcParty: true);
        var presentShip = RegisterForeign(harness, "someone_else", isNpcParty: false);

        harness.Members.Remove(Peer);
        harness.Broker.Publish(this, new MissionPeerLeft(Peer, Instance));
        GameThread.Run(() => { }, blocking: true);
        harness.Replicator.Tick(0.1f);
        harness.Engine.Verify(e => e.ParkHullAgents(departedShip.Hull), Times.Once);
        harness.Engine.Verify(e => e.RemoveHull(It.IsAny<MissionObject>()), Times.Never);
        harness.Replicator.Tick(0.1f);
        int transferred = harness.Replicator.TransferHostHulls("successor");

        harness.Engine.Verify(e => e.RemoveHull(departedShip.Hull), Times.Once);
        Assert.Equal(ShipLifecycle.Removed, harness.Registry.StateOf(departedShip.ShipId));
        Assert.Equal(1, transferred);
        Assert.Equal("successor", aiShip.CurrentAuthority);
        Assert.Equal("someone_else", presentShip.CurrentAuthority);
        Assert.Equal(1, harness.Replicator.DepartedHulls(BattleSideEnum.Defender));
    }

    // R41a-2: a departed party withdraws the tick after its hull removal finished, so the removal took its crew first and the
    // removal's unparked agents are back on physics; whether it hosted is read when the leave arrives.
    [Fact]
    public void MissionPeerLeft_WithdrawsThePartyTheTickAfterItsHullRemovalFinished()
    {
        using var mission = new MissionCurrentScope();
        var harness = new Harness(committed: true);
        harness.Session.Setup(s => s.IsHostController(Peer)).Returns(true);
        var departedShip = RegisterForeign(harness, Peer, isNpcParty: false);

        harness.Members.Remove(Peer);
        harness.Broker.Publish(this, new MissionPeerLeft(Peer, Instance));
        GameThread.Run(() => { }, blocking: true);
        harness.Session.Setup(s => s.IsHostController(Peer)).Returns(false);
        harness.Replicator.Tick(0.1f);
        harness.Replicator.Tick(0.1f);
        harness.Engine.Verify(e => e.RemoveHull(departedShip.Hull), Times.Once);
        harness.PartyWithdrawal.Verify(w => w.WithdrawParty(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        harness.Replicator.Tick(0.1f);
        harness.Replicator.Tick(0.1f);

        harness.PartyWithdrawal.Verify(w => w.WithdrawParty(Peer, true), Times.Once);
        harness.PartyWithdrawal.Verify(w => w.WithdrawParty(It.IsAny<string>(), It.IsAny<bool>()), Times.Once);
    }

    // R41a-2 C1: a returner re-entered before its old hulls settled; the withdrawal would select its fresh troops by party.
    [Fact]
    public void MissionPeerLeft_ReEntryBeforeTheHullsSettled_DropsThePartyWithdrawal()
    {
        using var mission = new MissionCurrentScope();
        var harness = new Harness(committed: true);
        RegisterForeign(harness, Peer, isNpcParty: false);

        harness.Members.Remove(Peer);
        harness.Broker.Publish(this, new MissionPeerLeft(Peer, Instance));
        GameThread.Run(() => { }, blocking: true);
        harness.Replicator.Tick(0.1f);
        harness.Members.Add(Peer);
        harness.Broker.Publish(this, new NetworkMissionPeerEntered(Peer, Instance));
        GameThread.Run(() => { }, blocking: true);
        harness.Replicator.Tick(0.1f);
        harness.Replicator.Tick(0.1f);
        harness.Replicator.Tick(0.1f);

        harness.PartyWithdrawal.Verify(w => w.WithdrawParty(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    private static NetworkShipInfo RegisterForeign(Harness harness, string owner, bool isNpcParty)
    {
        var hull = CreateHull();
        var ship = new NetworkShipInfo(Guid.NewGuid(), owner, "MapEventParty_" + owner, isNpcParty, hull, null);
        Assert.True(harness.Registry.TryRegister(ship));
        harness.LiveHulls.Add(hull);
        return ship;
    }

    [Theory]
    [InlineData(false, Own)]
    [InlineData(true, Peer)]
    public void HostMigrated_ToThisClient_MovesTheAiHullsOnlyWhileTheResultIsOpen(bool resultHeld, string expectedAuthority)
    {
        var harness = new Harness(committed: true, resultHeld: resultHeld);
        var aiHull = CreateHull();
        var ai = new NetworkShipInfo(Guid.NewGuid(), Peer, "MapEventParty_9", true, aiHull, null);
        harness.Registry.TryRegister(ai);
        harness.LiveHulls.Add(aiHull);

        harness.Broker.Publish(this, new BattleHostMigrated(Instance, Peer, Own));
        GameThread.Run(() => { }, blocking: true);

        Assert.Equal(expectedAuthority, ai.CurrentAuthority);
    }

    [Fact]
    public void ValidateSample_RejectsANonFiniteRudder()
    {
        var ship = new NetworkShipInfo(Guid.NewGuid(), Peer, null, false, CreateHull(), null);
        var sample = new NetworkBattleShipSample(ship.ShipId, Peer, 1,
            NetworkBattleShipSample.FromFrame(MatrixFrame.Identity), new BattleShipInput(0, 1, 0, float.NaN, 2));

        Assert.Equal("invalid_input", BattleShipReplicator.ValidateSample(ship, Own, sample, 0, 0, InMission));
    }

    [Fact]
    public void ValidateSample_RejectsAnInvalidRopeSet()
    {
        var ship = new NetworkShipInfo(Guid.NewGuid(), Peer, null, false, CreateHull(), null);
        var ropes = new[] { BattleRopeStateTests.Rope(1, BattleRopeState.RopesPulling), BattleRopeStateTests.Rope(2, BattleRopeState.Removed) };
        var sample = new NetworkBattleShipSample(ship.ShipId, Peer, 1,
            NetworkBattleShipSample.FromFrame(MatrixFrame.Identity), default, ropes);

        Assert.Equal("invalid_ropes", BattleShipReplicator.ValidateSample(ship, Own, sample, 0, 0, InMission));
    }

    [Fact]
    public void Tick_SendsTheOwnHullRopesWithEachSample()
    {
        var harness = new Harness(committed: true);
        var ropes = new[] { BattleRopeStateTests.Rope(1, BattleRopeState.RopeThrown) };
        harness.Engine.Setup(e => e.CaptureRopes(harness.OwnHull, It.IsAny<Func<MissionObject, Guid>>())).Returns(ropes);

        harness.Replicator.Tick(0.1f);

        harness.Network.Verify(n => n.SendAll(It.Is<IMessage>(m =>
            m is NetworkBattleShipSample && ((NetworkBattleShipSample)m).Ropes == ropes)), Times.Once);
    }

    [Fact]
    public void RopeTargets_ResolveTheLocalPlayersOwnHull()
    {
        var harness = new Harness(committed: true);
        var copy = CreateHull();
        var copyId = Guid.NewGuid();
        harness.Registry.TryRegister(new NetworkShipInfo(copyId, Peer, "MapEventParty_2", false, copy, null));

        // A peer's rope that hooked this client's hull targets it by the own hull's id.
        Assert.Same(harness.OwnHull, harness.Replicator.HullOf(harness.OwnShipId));
        Assert.Same(copy, harness.Replicator.HullOf(copyId));
        Assert.Equal(harness.OwnShipId, harness.Replicator.ShipIdOf(harness.OwnHull));
        Assert.Null(harness.Replicator.HullOf(Guid.NewGuid()));
        Assert.Equal(Guid.Empty, harness.Replicator.ShipIdOf(CreateHull()));
    }

    [Fact]
    public void SendFinalRopes_SendsTheOwnHullsFinalRopes()
    {
        var harness = new Harness(committed: true);
        var ropes = new[] { BattleRopeStateTests.Rope(2, BattleRopeState.BridgeConnected) };
        harness.Engine.Setup(e => e.CaptureRopes(harness.OwnHull, It.IsAny<Func<MissionObject, Guid>>())).Returns(ropes);
        harness.Replicator.Tick(0.1f);

        harness.Replicator.SendFinalRopes();

        harness.Network.Verify(n => n.SendAll(It.Is<IMessage>(m => m is NetworkBattleRopeFinal
            && ((NetworkBattleRopeFinal)m).ShipId == harness.OwnShipId
            && ((NetworkBattleRopeFinal)m).OwnerControllerId == Own
            && ((NetworkBattleRopeFinal)m).Ropes == ropes)), Times.Once);
    }

    [Fact]
    public void SendFinalRopes_WithoutRopes_SendsNoFinalState()
    {
        var harness = new Harness(committed: true);
        harness.Replicator.Tick(0.1f);

        harness.Replicator.SendFinalRopes();

        harness.Network.Verify(n => n.SendAll(It.Is<IMessage>(m => m is NetworkBattleRopeFinal)), Times.Never);
    }

    [Fact]
    public void SendFinalRopes_BeforeTheHullsWereAnnounced_SendsNoFinalState()
    {
        var harness = new Harness(committed: false);
        harness.Engine.Setup(e => e.CaptureRopes(harness.OwnHull, It.IsAny<Func<MissionObject, Guid>>()))
            .Returns(new[] { BattleRopeStateTests.Rope(1, BattleRopeState.RopeThrown) });
        harness.Replicator.Tick(0.1f);

        harness.Replicator.SendFinalRopes();

        harness.Network.Verify(n => n.SendAll(It.IsAny<IMessage>()), Times.Never);
    }

    [Theory]
    [InlineData(Peer, Peer, true, null)]
    [InlineData(Peer, "someone_else", true, "not_authority")]
    [InlineData(Own, Own, true, "own_hull")]
    [InlineData(Peer, Peer, false, "invalid_ropes")]
    public void ValidateFinalRopes_AcceptsOnlyTheHullAuthoritysValidRopes(string authority, string sender, bool valid, string expected)
    {
        var ship = new NetworkShipInfo(Guid.NewGuid(), authority, null, false, CreateHull(), null);
        var rope = BattleRopeStateTests.Rope(3, BattleRopeState.Removed);
        if (!valid) rope.Generation = 0;

        var final = new NetworkBattleRopeFinal(ship.ShipId, sender, new[] { rope });

        Assert.Equal(expected, BattleShipReplicator.ValidateFinalRopes(ship, Own, final, InMission));
    }

    [Fact]
    public void Inspect_ReportsEachHullsHelmInput()
    {
        var harness = new Harness(committed: true);
        harness.Engine.Setup(e => e.ReadInput(harness.OwnHull)).Returns(new BattleShipInput(1, 2, 0, -0.5f, 2));

        var ship = JObject.FromObject(harness.Replicator.Inspect())["ships"]![0]!;

        Assert.Equal(-0.5f, (float)ship["input"]!["Rudder"]!);
        Assert.Equal(2, (int)ship["input"]!["RowerLongitudinal"]!);
        Assert.Equal(2, (int)ship["input"]!["Sail"]!);
    }

    [Fact]
    public void Sample_RoundTripsTheHelmInput()
    {
        var input = new BattleShipInput(1, 2, 0, -0.5f, 2);
        var sample = new NetworkBattleShipSample(Guid.NewGuid(), Peer, 7,
            NetworkBattleShipSample.FromFrame(MatrixFrame.Identity), input);

        using var stream = new MemoryStream();
        Serializer.Serialize(stream, sample);
        stream.Position = 0;
        var copy = Serializer.Deserialize<NetworkBattleShipSample>(stream);

        Assert.Equal(input, copy.Input);
        Assert.Equal(sample.Frame, copy.Frame);
        Assert.Equal(7, copy.Sequence);
    }

    // M10: copies extrapolate the owner's frame with the body velocity and yaw rate the sample carries.
    [Fact]
    public void Sample_RoundTripsTheOwnersBodyMotion()
    {
        var motion = new BattleShipMotion(new Vec2(4.5f, -1.25f), 0.2f);
        var sample = new NetworkBattleShipSample(Guid.NewGuid(), Peer, 7,
            NetworkBattleShipSample.FromFrame(MatrixFrame.Identity), motion: motion);

        var copy = Serializer.DeepClone(sample);

        Assert.Equal(4.5f, copy.Motion.Velocity.x);
        Assert.Equal(-1.25f, copy.Motion.Velocity.y);
        Assert.Equal(0.2f, copy.Motion.YawRate);
    }

    // R45 roll: copies heel under the owner's crew load, which rides each sample as mass and a hull-local centre.
    [Fact]
    public void Sample_RoundTripsTheOwnersCrewLoad()
    {
        var load = BattleShipCrewLoad.Create(480f, new Vec3(0.75f, -6.5f, 2.25f));
        var sample = new NetworkBattleShipSample(Guid.NewGuid(), Peer, 7,
            NetworkBattleShipSample.FromFrame(MatrixFrame.Identity), motion: new BattleShipMotion(new Vec2(1f, 2f), 0.1f, load));

        var copy = Serializer.DeepClone(sample);

        Assert.Equal(480f, copy.Motion.CrewLoad.Mass);
        Assert.Equal(0.75f, copy.Motion.CrewLoad.LocalCentre.x);
        Assert.Equal(-6.5f, copy.Motion.CrewLoad.LocalCentre.y);
        Assert.Equal(2.25f, copy.Motion.CrewLoad.LocalCentre.z);
    }

    // An older sender writes no crew fields; the copy then keeps vanilla's own crew weight.
    [Fact]
    public void Sample_WithoutACrewLoad_ArrivesAsUnknown()
    {
        var sample = new NetworkBattleShipSample(Guid.NewGuid(), Peer, 7,
            NetworkBattleShipSample.FromFrame(MatrixFrame.Identity), motion: new BattleShipMotion(new Vec2(1f, 2f), 0.1f));

        var copy = Serializer.DeepClone(sample);

        Assert.False(copy.HasCrewLoad);
        Assert.Null(copy.Motion.CrewLoad);
    }

    // A crew that left the deck (all boarded an enemy hull) is a known-empty load, not an unknown one.
    [Fact]
    public void Sample_RoundTripsAKnownEmptyDeck()
    {
        var sample = new NetworkBattleShipSample(Guid.NewGuid(), Peer, 7, NetworkBattleShipSample.FromFrame(MatrixFrame.Identity),
            motion: new BattleShipMotion(new Vec2(1f, 2f), 0.1f, BattleShipCrewLoad.Empty));

        var copy = Serializer.DeepClone(sample);

        Assert.True(copy.HasCrewLoad);
        Assert.Same(BattleShipCrewLoad.Empty, copy.Motion.CrewLoad);
    }

    [Fact]
    public void CrewLoad_IsEmptyForAZeroMass()
    {
        Assert.Same(BattleShipCrewLoad.Empty, BattleShipCrewLoad.Create(0f, new Vec3(1f, 2f, 3f)));
        Assert.True(BattleShipCrewLoad.Empty.IsEmpty);
    }

    [Theory]
    [InlineData(-80f, 0f, 0f, 0f)]
    [InlineData(float.NaN, 0f, 0f, 0f)]
    [InlineData(float.PositiveInfinity, 0f, 0f, 0f)]
    [InlineData(80f, float.NaN, 0f, 0f)]
    [InlineData(80f, 0f, float.NegativeInfinity, 0f)]
    [InlineData(80f, 0f, 0f, float.NaN)]
    public void CrewLoad_IsUnknownForANegativeOrNonFiniteLoad(float mass, float x, float y, float z)
    {
        Assert.Null(BattleShipCrewLoad.Create(mass, new Vec3(x, y, z)));
    }

    // A NaN crew mass on the wire reads as unknown; it never costs the copy its frame.
    [Fact]
    public void ValidateSample_AcceptsANonFiniteCrewLoad_AsUnknown()
    {
        var ship = new NetworkShipInfo(Guid.NewGuid(), Peer, null, false, CreateHull(), null);
        var sample = Serializer.DeepClone(new NetworkBattleShipSample(ship.ShipId, Peer, 1,
            NetworkBattleShipSample.FromFrame(MatrixFrame.Identity),
            motion: new BattleShipMotion(new Vec2(1f, 2f), 0.1f, BattleShipCrewLoad.Create(80f, Vec3.Zero))));
        typeof(NetworkBattleShipSample).GetField(nameof(NetworkBattleShipSample.CrewMass))!.SetValue(sample, float.NaN);

        Assert.Null(BattleShipReplicator.ValidateSample(ship, Own, sample, 0, 0, InMission));
        Assert.Null(sample.Motion.CrewLoad);
    }

    [Theory]
    [InlineData(float.NaN, 0f, 0f)]
    [InlineData(0f, float.PositiveInfinity, 0f)]
    [InlineData(0f, 0f, float.NegativeInfinity)]
    public void ValidateSample_RejectsANonFiniteMotion(float velocityX, float velocityY, float yawRate)
    {
        var ship = new NetworkShipInfo(Guid.NewGuid(), Peer, null, false, CreateHull(), null);
        var sample = new NetworkBattleShipSample(ship.ShipId, Peer, 1, NetworkBattleShipSample.FromFrame(MatrixFrame.Identity),
            motion: new BattleShipMotion(new Vec2(velocityX, velocityY), yawRate));

        Assert.Equal("invalid_motion", BattleShipReplicator.ValidateSample(ship, Own, sample, 0, 0, InMission));
    }

    [Fact]
    public void Tick_SendsTheOwnHullsBodyMotion_AndACopyHandsTheSampleToTheEngine()
    {
        var harness = new Harness(committed: true);
        var motion = new BattleShipMotion(new Vec2(3f, 1f), -0.1f);
        harness.Engine.Setup(e => e.ReadMotion(harness.OwnHull)).Returns(motion);
        var (copy, ship) = RegisterPeerCopy(harness, isNpcParty: false);

        harness.Replicator.Tick(0.1f);
        harness.Replicator.AcceptSample(new NetworkBattleShipSample(ship.ShipId, Peer, 1,
            NetworkBattleShipSample.FromFrame(MatrixFrame.Identity), motion: motion));

        harness.Network.Verify(n => n.SendAll(It.Is<IMessage>(m => m is NetworkBattleShipSample
            && ((NetworkBattleShipSample)m).VelocityX == 3f && ((NetworkBattleShipSample)m).YawRate == -0.1f)), Times.Once);
        harness.Engine.Verify(e => e.ApplyForeignSample(copy, It.IsAny<MatrixFrame>(),
            It.Is<BattleShipMotion>(m => m.Velocity.x == 3f && m.Velocity.y == 1f && m.YawRate == -0.1f)), Times.Once);
    }

    [Fact]
    public void SpawnRecord_RoundTripsTheHullDescriptor()
    {
        var record = new BattleShipSpawnData(Guid.NewGuid(), Peer, "MapEventParty_1", false, BattleSideEnum.Defender, 2,
            NetworkBattleShipSample.FromFrame(MatrixFrame.Identity), "northern_light_ship", new[] { "sail" },
            new[] { "sail_piece" }, "figurehead_lion", "Sea Wolf", 900f, 400f, 1234, "pattern");

        using var stream = new MemoryStream();
        Serializer.Serialize(stream, new NetworkSpawnBattleShips(new[] { record }));
        stream.Position = 0;
        var copy = Assert.Single(Serializer.Deserialize<NetworkSpawnBattleShips>(stream).Ships);

        Assert.Equal(record.ShipId, copy.ShipId);
        Assert.Equal(record.OwnerControllerId, copy.OwnerControllerId);
        Assert.Equal(record.Side, copy.Side);
        Assert.Equal(record.FormationIndex, copy.FormationIndex);
        Assert.Equal(record.Frame, copy.Frame);
        Assert.Equal(record.HullId, copy.HullId);
        Assert.Equal(record.PieceSlots, copy.PieceSlots);
        Assert.Equal(record.PieceIds, copy.PieceIds);
        Assert.Equal(record.FigureheadId, copy.FigureheadId);
        Assert.Equal(record.Name, copy.Name);
        Assert.Equal(record.HitPoints, copy.HitPoints);
        Assert.Equal(record.SailHitPoints, copy.SailHitPoints);
        Assert.Equal(record.RandomValue, copy.RandomValue);
        Assert.Equal(record.CustomSailPatternId, copy.CustomSailPatternId);
    }

    // Capture 2.6: a joiner holding the old authority's record of a hull lets a record of a later capture replace it, never
    // the other way round, so the copy spawns as the captor's.
    [Theory]
    [InlineData(1, 0, true)]
    [InlineData(2, 1, true)]
    [InlineData(1, 1, false)]
    [InlineData(0, 1, false)]
    public void IsNewerRecord_OnlyALaterCaptureReplacesAHeldRecord(int recordSequence, int heldSequence, bool expected)
    {
        Assert.Equal(expected, BattleShipReplicator.IsNewerRecord(Record(recordSequence), Record(heldSequence)));

        static BattleShipSpawnData Record(int sequence) => new BattleShipSpawnData(Guid.Empty, Peer, "MapEventParty_1", true,
            BattleSideEnum.Defender, 0, NetworkBattleShipSample.FromFrame(MatrixFrame.Identity), "northern_light_ship", null, null,
            null, null, 900f, 400f, 1, "", captorControllerId: sequence > 0 ? "captor" : null, captureSequence: sequence);
    }

    // An AI hull a later host simulates keeps naming the host that fielded it.
    [Fact]
    public void SpawnRecord_RoundTripsTheOriginalOwner_AndAnOwnRecordNamesItsOwner()
    {
        var migrated = new BattleShipSpawnData(Guid.NewGuid(), "host", "MapEventParty_1", true, BattleSideEnum.Defender, 2,
            NetworkBattleShipSample.FromFrame(MatrixFrame.Identity), "northern_light_ship", null, null, null, null, 900f, 400f, 1,
            "", Peer);
        var own = new BattleShipSpawnData(Guid.NewGuid(), Peer, "MapEventParty_1", false, BattleSideEnum.Defender, 2,
            NetworkBattleShipSample.FromFrame(MatrixFrame.Identity), "northern_light_ship", null, null, null, null, 900f, 400f, 1,
            "", Peer);

        var copies = Serializer.DeepClone(new NetworkSpawnBattleShips(new[] { migrated, own })).Ships;

        Assert.Equal(Peer, copies[0].OriginalOwner);
        Assert.Equal(Peer, copies[1].OriginalOwner);
        Assert.Null(copies[1].OriginalOwnerControllerId);
    }

    // M6 D1: a copy needs its campaign ship's id to report the ship's fate after it becomes the hull's authority.
    [Fact]
    public void SpawnRecord_RoundTripsTheCampaignShipId()
    {
        var record = new BattleShipSpawnData(Guid.NewGuid(), "host", "MapEventParty_1", true, BattleSideEnum.Defender, 2,
            NetworkBattleShipSample.FromFrame(MatrixFrame.Identity), "northern_light_ship", null, null, null, null, 900f, 400f, 1,
            "", Peer, "Ship_7");
        var withoutShip = new BattleShipSpawnData(Guid.NewGuid(), Peer, "MapEventParty_1", false, BattleSideEnum.Defender, 2,
            NetworkBattleShipSample.FromFrame(MatrixFrame.Identity), "northern_light_ship", null, null, null, null, 900f, 400f, 1,
            "");

        var copies = Serializer.DeepClone(new NetworkSpawnBattleShips(new[] { record, withoutShip })).Ships;

        Assert.Equal("Ship_7", copies[0].CampaignShipId);
        Assert.Null(copies[1].CampaignShipId);
    }

    [Fact]
    public void Tick_SendsTheOwnHullConditionOnceAndThenOnlyWhenItChanges()
    {
        var harness = new Harness(committed: true);
        var condition = Condition(900f);
        harness.Engine.Setup(e => e.ReadCondition(harness.OwnHull)).Returns(() => condition);

        harness.Replicator.Tick(0.1f);
        harness.Replicator.Tick(0.1f);
        condition = Condition(900.001f);
        harness.Replicator.Tick(0.1f);
        condition = Condition(850f);
        harness.Replicator.Tick(0.1f);

        harness.Network.Verify(n => n.SendAll(It.Is<IMessage>(m => m is NetworkShipCondition)), Times.Exactly(2));
        harness.Network.Verify(n => n.SendAll(It.Is<IMessage>(m => m is NetworkShipCondition
            && ((NetworkShipCondition)m).Revision == 2 && ((NetworkShipCondition)m).Condition.HitPoints == 850f
            && ((NetworkShipCondition)m).OwnerControllerId == Own && ((NetworkShipCondition)m).HostEpoch == HostEpoch)), Times.Once);
    }

    [Fact]
    public void Tick_BeforeDeploymentCommit_SendsNoCondition()
    {
        var harness = new Harness(committed: false);
        harness.Engine.Setup(e => e.ReadCondition(harness.OwnHull)).Returns(Condition(900f));

        harness.Replicator.Tick(0.1f);

        harness.Network.Verify(n => n.SendAll(It.Is<IMessage>(m => m is NetworkShipCondition)), Times.Never);
    }

    [Theory]
    [InlineData(Peer, Peer, false, 5, 0, 4, 0, null)]
    [InlineData(Peer, "someone_else", false, 5, 0, 4, 0, "not_authority")]
    [InlineData(Own, Own, false, 5, 0, 4, 0, "own_hull")]
    [InlineData(Peer, Peer, false, 4, 0, 4, 0, "stale")]
    [InlineData(Peer, Peer, false, 3, 9, 4, 1, "stale")]
    [InlineData(Peer, Peer, true, 9, 2, 1, 3, "stale_epoch")]
    [InlineData(Peer, Peer, true, 1, 4, 9, 3, null)]
    [InlineData(Peer, Peer, true, 9, 3, 9, 3, "stale")]
    public void ValidateCondition_KeepsTheOwnersRevisionOrderAndRestartsItForANewHostEpoch(string authority, string sender,
        bool isNpcParty, long revision, int epoch, long acceptedRevision, int acceptedEpoch, string expected)
    {
        var ship = new NetworkShipInfo(Guid.NewGuid(), authority, "MapEventParty_9", isNpcParty, CreateHull(), null);
        var condition = new NetworkShipCondition(ship.ShipId, sender, revision, epoch, Condition(500f));

        Assert.Equal(expected, BattleShipConditionStream.ValidateCondition(ship, Own, condition, acceptedRevision, acceptedEpoch, InMission));
    }

    [Fact]
    public void ValidateCondition_RejectsANonFiniteCondition()
    {
        var ship = new NetworkShipInfo(Guid.NewGuid(), Peer, null, false, CreateHull(), null);
        var condition = new NetworkShipCondition(ship.ShipId, Peer, 1, 0, Condition(float.NaN));

        Assert.Equal("invalid_condition", BattleShipConditionStream.ValidateCondition(ship, Own, condition, 0, 0, InMission));
    }

    [Fact]
    public void AcceptCondition_AppliesNewerRevisionsToTheCopyAndDropsOlderOnes()
    {
        var harness = new Harness(committed: true);
        var copy = CreateHull();
        var ship = new NetworkShipInfo(Guid.NewGuid(), Peer, "MapEventParty_2", false, copy, null);
        harness.Registry.TryRegister(ship);
        var newer = new NetworkShipCondition(ship.ShipId, Peer, 2, 0, Condition(600f));
        var older = new NetworkShipCondition(ship.ShipId, Peer, 1, 0, Condition(800f));

        harness.Replicator.AcceptCondition(newer);
        harness.Replicator.AcceptCondition(older);

        harness.Engine.Verify(e => e.ApplyCondition(copy, newer.Condition), Times.Once);
        harness.Engine.Verify(e => e.ApplyCondition(copy, older.Condition), Times.Never);
        var inspected = JObject.FromObject(harness.Replicator.Inspect())["ships"]!
            .Single(entry => (Guid)entry["shipId"]! == ship.ShipId);
        Assert.Equal(2, (long)inspected["conditionRevision"]!);
        Assert.Equal("condition_stale", (string)inspected["lastReject"]!);
    }

    [Fact]
    public void AcceptCondition_ForAHullNotSpawnedYet_AppliesTheLatestOneOnceItIsRegistered()
    {
        var harness = new Harness(committed: true);
        var shipId = Guid.NewGuid();
        var latest = new NetworkShipCondition(shipId, Peer, 3, 0, Condition(400f));
        harness.Replicator.AcceptCondition(latest);
        harness.Replicator.AcceptCondition(new NetworkShipCondition(shipId, Peer, 2, 0, Condition(700f)));
        harness.Replicator.Tick(0.1f);
        harness.Engine.Verify(e => e.ApplyCondition(It.IsAny<MissionObject>(), It.IsAny<BattleShipCondition>()), Times.Never);

        var copy = CreateHull();
        harness.Registry.TryRegister(new NetworkShipInfo(shipId, Peer, "MapEventParty_2", false, copy, null));
        harness.LiveHulls.Add(copy);
        harness.Replicator.Tick(0.1f);

        harness.Engine.Verify(e => e.ApplyCondition(copy, latest.Condition), Times.Once);
        harness.Engine.Verify(e => e.ApplyCondition(It.IsAny<MissionObject>(), It.IsAny<BattleShipCondition>()), Times.Once);
    }

    [Fact]
    public void Inspect_ReportsEachHullsCondition()
    {
        var harness = new Harness(committed: true);
        harness.Engine.Setup(e => e.ReadCondition(harness.OwnHull))
            .Returns(new BattleShipCondition(420f, 80f, 30f, new float[6], 1));
        harness.Replicator.Tick(0.1f);

        var ship = JObject.FromObject(harness.Replicator.Inspect())["ships"]![0]!;

        Assert.Equal(420f, (float)ship["hp"]!);
        Assert.Equal(80f, (float)ship["sailHp"]!);
        Assert.Equal(1, (int)ship["sinking"]!);
        Assert.Equal(1, (long)ship["conditionRevision"]!);
    }

    [Fact]
    public void Tick_StopsSamplingAnOwnHullAfterItsSunkConditionWentOut()
    {
        var harness = new Harness(committed: true);
        var condition = Condition(900f);
        harness.Engine.Setup(e => e.ReadCondition(harness.OwnHull)).Returns(() => condition);

        harness.Replicator.Tick(0.1f);
        condition = new BattleShipCondition(0f, 0f, 0f, new float[6], BattleShipCondition.Sunk);
        harness.Replicator.Tick(0.1f);
        harness.Replicator.Tick(0.1f);
        harness.Replicator.Tick(0.1f);

        harness.Network.Verify(n => n.SendAll(It.Is<IMessage>(m => m is NetworkBattleShipSample)), Times.Exactly(2));
        harness.Network.Verify(n => n.SendAll(It.Is<IMessage>(m => m is NetworkShipCondition
            && ((NetworkShipCondition)m).Condition.SinkingState == BattleShipCondition.Sunk)), Times.Once);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(BattleShipCondition.Sunk, true)]
    public void Inspect_ReportsWhetherEachHullHasSunk(int sinkingState, bool expected)
    {
        var harness = new Harness(committed: true);
        harness.Engine.Setup(e => e.ReadCondition(harness.OwnHull))
            .Returns(new BattleShipCondition(0f, 0f, 0f, new float[6], sinkingState));

        var ship = JObject.FromObject(harness.Replicator.Inspect())["ships"]![0]!;

        Assert.Equal(expected, (bool)ship["isSunk"]!);
    }

    [Fact]
    public void ReplaceNpcHull_RefusesASunkHull()
    {
        var harness = new Harness(committed: true);
        var ai = new NetworkShipInfo(Guid.NewGuid(), Own, "MapEventParty_8", true, CreateHull(), null);
        harness.Registry.TryRegister(ai);
        harness.Engine.Setup(e => e.ReadCondition(ai.Hull)).Returns(new BattleShipCondition(0f, 0f, 0f, new float[6], BattleShipCondition.Sunk));

        Assert.Equal("The hull has sunk.", harness.Replicator.ReplaceNpcHull(ai.ShipId));
    }

    [Fact]
    public void Condition_DiffersOnlyBeyondTheToleranceOrOnSinkingState()
    {
        var condition = new BattleShipCondition(500f, 200f, 100f, new[] { 50f, 50f }, 0);

        Assert.True(condition.DiffersFrom(null));
        Assert.False(condition.DiffersFrom(new BattleShipCondition(500.005f, 200f, 100f, new[] { 50f, 50f }, 0)));
        Assert.True(condition.DiffersFrom(new BattleShipCondition(500f, 200f, 100f, new[] { 50f, 49f }, 0)));
        Assert.True(condition.DiffersFrom(new BattleShipCondition(500f, 200f, 100f, new[] { 50f, 50f }, 1)));
    }

    [Fact]
    public void NetworkShipCondition_RoundTripsEveryField()
    {
        var message = new NetworkShipCondition(Guid.NewGuid(), Peer, 7, 2,
            new BattleShipCondition(420.5f, 80f, -3f, new[] { 1f, 2f, 3f, 4f, 5f, 6f }, 2));

        using var stream = new MemoryStream();
        Serializer.Serialize(stream, message);
        stream.Position = 0;
        var copy = Serializer.Deserialize<NetworkShipCondition>(stream);

        Assert.Equal(message.ShipId, copy.ShipId);
        Assert.Equal(Peer, copy.OwnerControllerId);
        Assert.Equal(7, copy.Revision);
        Assert.Equal(2, copy.HostEpoch);
        Assert.Equal(420.5f, copy.Condition.HitPoints);
        Assert.Equal(80f, copy.Condition.SailHitPoints);
        Assert.Equal(-3f, copy.Condition.FireHitPoints);
        Assert.Equal(message.Condition.PartialHitPoints, copy.Condition.PartialHitPoints);
        Assert.Equal(2, copy.Condition.SinkingState);
    }

    [Fact]
    public void AcceptSpawnRecords_DuringLocalDeployment_HoldsTheCopyAndSpawnsItOnceAfterwardWithTheLatestState()
    {
        using var mission = new MissionCurrentScope();
        var harness = new Harness(committed: true);
        bool deploying = true;
        harness.Engine.SetupGet(e => e.IsDeploymentMode).Returns(() => deploying);
        var copy = harness.SetupForeignSpawn();
        var record = ForeignRecord(Guid.NewGuid());
        var frame = NetworkBattleShipSample.FromFrame(MatrixFrame.Identity);
        var latestCondition = new NetworkShipCondition(record.ShipId, Peer, 2, 0, Condition(500f));

        harness.Replicator.AcceptSpawnRecords(new[] { record });
        harness.Replicator.AcceptSample(new NetworkBattleShipSample(record.ShipId, Peer, 1, frame));
        harness.Replicator.AcceptSample(new NetworkBattleShipSample(record.ShipId, Peer, 3, frame));
        harness.Replicator.AcceptSample(new NetworkBattleShipSample(record.ShipId, Peer, 2, frame));
        harness.Replicator.AcceptCondition(new NetworkShipCondition(record.ShipId, Peer, 1, 0, Condition(800f)));
        harness.Replicator.AcceptCondition(latestCondition);
        harness.Replicator.Tick(0.1f);
        harness.Replicator.Tick(0.1f);
        var whileDeploying = JObject.FromObject(harness.Replicator.Inspect());

        deploying = false;
        harness.Replicator.Tick(0.016f);
        harness.Replicator.Tick(0.016f);

        Assert.Equal(1, (int)whileDeploying["heldForDeployment"]!);
        Assert.Equal(1, (int)whileDeploying["heldSamples"]!);
        harness.Engine.Verify(e => e.SpawnForeignHull(record, It.IsAny<Team>(), out It.Ref<Formation>.IsAny), Times.Once);
        harness.Engine.Verify(e => e.ApplyInput(copy, It.IsAny<BattleShipInput>()), Times.Once);
        harness.Engine.Verify(e => e.ApplyCondition(copy, latestCondition.Condition), Times.Once);
        harness.Engine.Verify(e => e.ApplyCondition(It.IsAny<MissionObject>(), It.IsAny<BattleShipCondition>()), Times.Once);
        var inspected = Inspected(harness, record.ShipId);
        Assert.Equal(3, (long)inspected["accepted"]!);
        Assert.Equal(0, (long)inspected["rejects"]!);
        Assert.Equal(0, (int)JObject.FromObject(harness.Replicator.Inspect())["heldSamples"]!);
    }

    [Fact]
    public void AcceptSpawnRecords_AiRecordHeldThroughAHostMigrationToThisClient_TakesTheHullOverOnceItSpawns()
    {
        using var mission = new MissionCurrentScope();
        var harness = new Harness(committed: true);
        bool deploying = true;
        harness.Engine.SetupGet(e => e.IsDeploymentMode).Returns(() => deploying);
        var copy = harness.SetupForeignSpawn();
        var record = AiRecord(Guid.NewGuid());
        var heldCondition = new NetworkShipCondition(record.ShipId, Peer, 4, HostEpoch - 1, Condition(420f));

        harness.Replicator.AcceptSpawnRecords(new[] { record });
        harness.Replicator.AcceptSample(new NetworkBattleShipSample(record.ShipId, Peer, 5, MovedFrame(), hostEpoch: HostEpoch - 1));
        harness.Replicator.AcceptCondition(heldCondition);
        harness.Replicator.Tick(0.1f);
        harness.Broker.Publish(this, new BattleHostMigrated(Instance, Peer, Own));
        GameThread.Run(() => { }, blocking: true);

        deploying = false;
        harness.Replicator.Tick(0.016f);
        harness.Replicator.Tick(0.016f);

        Assert.True(harness.Registry.TryGet(record.ShipId, out var ship));
        Assert.Equal(Own, ship.CurrentAuthority);
        Assert.Equal(Peer, ship.OriginalOwner);
        harness.Engine.Verify(e => e.ApplyCondition(copy, heldCondition.Condition), Times.Once);
        harness.Engine.Verify(e => e.ApplyForeignSample(copy, It.Is<MatrixFrame>(f => f.origin.x == 5f), It.IsAny<BattleShipMotion>()),
            Times.Once);
        harness.Engine.Verify(e => e.ParkHullAgents(copy), Times.Once);
    }

    [Fact]
    public void AcceptSpawnRecords_AiRecordHeldThroughAHostMigrationToThisClient_KeepsAHullThatSankWhileHeldSunk()
    {
        using var mission = new MissionCurrentScope();
        var harness = new Harness(committed: true);
        bool deploying = true;
        harness.Engine.SetupGet(e => e.IsDeploymentMode).Returns(() => deploying);
        var copy = harness.SetupForeignSpawn();
        BattleShipCondition applied = null;
        harness.Engine.Setup(e => e.ApplyCondition(copy, It.IsAny<BattleShipCondition>()))
            .Callback((MissionObject _, BattleShipCondition condition) => applied = condition);
        harness.Engine.Setup(e => e.ReadCondition(copy)).Returns(() => applied);
        var record = AiRecord(Guid.NewGuid());
        var sunk = new NetworkShipCondition(record.ShipId, Peer, 4, HostEpoch - 1,
            new BattleShipCondition(0f, 0f, 0f, new float[6], BattleShipCondition.Sunk));

        harness.Replicator.AcceptSpawnRecords(new[] { record });
        harness.Replicator.AcceptCondition(sunk);
        harness.Broker.Publish(this, new BattleHostMigrated(Instance, Peer, Own));
        GameThread.Run(() => { }, blocking: true);

        deploying = false;
        harness.Replicator.Tick(0.016f);
        harness.Replicator.Tick(0.016f);

        Assert.True(harness.Registry.TryGet(record.ShipId, out var ship));
        Assert.Equal(Own, ship.CurrentAuthority);
        harness.Engine.Verify(e => e.ApplyCondition(copy, sunk.Condition), Times.Once);
        harness.Engine.Verify(e => e.ParkHullAgents(It.IsAny<MissionObject>()), Times.Never);
    }

    [Fact]
    public void AcceptSpawnRecords_AiRecordHeldThroughAHostMigrationToAPeer_FollowsTheNewHostsSamples()
    {
        using var mission = new MissionCurrentScope();
        var harness = new Harness(committed: true);
        bool deploying = true;
        harness.Engine.SetupGet(e => e.IsDeploymentMode).Returns(() => deploying);
        var copy = harness.SetupForeignSpawn();
        var record = AiRecord(Guid.NewGuid());
        var frame = NetworkBattleShipSample.FromFrame(MatrixFrame.Identity);
        var heldCondition = new NetworkShipCondition(record.ShipId, Peer, 4, HostEpoch - 1,
            new BattleShipCondition(0f, 0f, 0f, new float[6], BattleShipCondition.Sunk));

        harness.Replicator.AcceptSpawnRecords(new[] { record });
        harness.Replicator.AcceptCondition(heldCondition);
        harness.Replicator.TransferHostHulls("successor");
        deploying = false;
        harness.Replicator.Tick(0.016f);
        harness.Replicator.AcceptSample(new NetworkBattleShipSample(record.ShipId, Peer, 1, frame, hostEpoch: HostEpoch));
        harness.Replicator.AcceptSample(new NetworkBattleShipSample(record.ShipId, "successor", 1, frame, hostEpoch: HostEpoch));

        Assert.True(harness.Registry.TryGet(record.ShipId, out var ship));
        Assert.Equal("successor", ship.CurrentAuthority);
        harness.Engine.Verify(e => e.ApplyCondition(copy, heldCondition.Condition), Times.Once);
        harness.Engine.Verify(e => e.ApplyInput(copy, It.IsAny<BattleShipInput>()), Times.Once);
        harness.Engine.Verify(e => e.ParkHullAgents(It.IsAny<MissionObject>()), Times.Never);
        var inspected = Inspected(harness, record.ShipId);
        Assert.Equal(1, (long)inspected["accepted"]!);
        Assert.Equal(1, (long)inspected["rejects"]!);
    }

    [Fact]
    public void AcceptSpawnRecords_BeforeTheInitialShipsAreDeployed_WaitsEvenOutsideDeploymentMode()
    {
        using var mission = new MissionCurrentScope();
        var harness = new Harness(committed: true);
        bool deployed = false;
        harness.SetupForeignSpawn(initialShipsDeployed: false);
        harness.Deployment.SetupGet(d => d.IsTeamSetupOver).Returns(() => deployed);
        var record = ForeignRecord(Guid.NewGuid());

        harness.Replicator.AcceptSpawnRecords(new[] { record });
        harness.Replicator.Tick(0.1f);
        harness.Engine.Verify(e => e.SpawnForeignHull(It.IsAny<BattleShipSpawnData>(), It.IsAny<Team>(), out It.Ref<Formation>.IsAny),
            Times.Never);

        deployed = true;
        harness.Replicator.Tick(0.1f);
        harness.Engine.Verify(e => e.SpawnForeignHull(record, It.IsAny<Team>(), out It.Ref<Formation>.IsAny), Times.Once);
    }

    [Fact]
    public void AcceptSpawnRecords_MarksAWaitingRecordAwaitingInTheRegistryUntilItsCopySpawns()
    {
        using var mission = new MissionCurrentScope();
        var harness = new Harness(committed: true);
        bool deploying = true;
        harness.Engine.SetupGet(e => e.IsDeploymentMode).Returns(() => deploying);
        harness.SetupForeignSpawn();
        var record = ForeignRecord(Guid.NewGuid());

        harness.Replicator.AcceptSpawnRecords(new[] { record });
        var whileHeld = harness.Registry.StateOf(record.ShipId);
        deploying = false;
        harness.Replicator.Tick(0.016f);

        Assert.Equal(ShipLifecycle.Awaiting, whileHeld);
        Assert.Equal(ShipLifecycle.Registered, harness.Registry.StateOf(record.ShipId));
    }

    [Fact]
    public void AcceptSpawnRecords_ARecordThatNeverSpawns_EndsSpawnFailedAndDropsItsHeldSample()
    {
        using var mission = new MissionCurrentScope();
        var harness = new Harness(committed: true);
        harness.SetupForeignSpawn();
        Formation formation = null!;
        harness.Engine.Setup(e => e.SpawnForeignHull(It.IsAny<BattleShipSpawnData>(), It.IsAny<Team>(), out formation))
            .Returns((MissionObject)null!);
        var record = ForeignRecord(Guid.NewGuid());
        harness.Replicator.AcceptSpawnRecords(new[] { record });
        harness.Replicator.AcceptSample(new NetworkBattleShipSample(record.ShipId, Peer, 1,
            NetworkBattleShipSample.FromFrame(MatrixFrame.Identity)));

        for (int attempt = 0; attempt < BattleShipReplicator.ForeignHullSpawnAttempts; attempt++)
            harness.Replicator.Tick(BattleShipReplicator.ForeignHullRetrySeconds);

        var inspected = JObject.FromObject(harness.Replicator.Inspect());
        Assert.Equal(ShipLifecycle.SpawnFailed, harness.Registry.StateOf(record.ShipId));
        Assert.Equal(0, (int)inspected["pendingForeignHulls"]!);
        Assert.Equal(0, (int)inspected["heldSamples"]!);
        harness.Engine.Verify(e => e.SpawnForeignHull(record, It.IsAny<Team>(), out It.Ref<Formation>.IsAny),
            Times.Exactly(BattleShipReplicator.ForeignHullSpawnAttempts));
    }

    [Fact]
    public void AcceptSpawnRecords_ARepeatedRecordWhileTheFirstWaits_SpawnsOneCopy()
    {
        using var mission = new MissionCurrentScope();
        var harness = new Harness(committed: true);
        bool deploying = true;
        harness.Engine.SetupGet(e => e.IsDeploymentMode).Returns(() => deploying);
        harness.SetupForeignSpawn();
        var record = ForeignRecord(Guid.NewGuid());
        harness.Replicator.AcceptSpawnRecords(new[] { record });
        harness.Replicator.AcceptSpawnRecords(new[] { ForeignRecord(record.ShipId) });
        int waiting = (int)JObject.FromObject(harness.Replicator.Inspect())["pendingForeignHulls"]!;

        deploying = false;
        harness.Replicator.Tick(0.016f);

        Assert.Equal(1, waiting);
        harness.Engine.Verify(e => e.SpawnForeignHull(record, It.IsAny<Team>(), out It.Ref<Formation>.IsAny), Times.Once);
        harness.Engine.Verify(e => e.SpawnForeignHull(It.IsAny<BattleShipSpawnData>(), It.IsAny<Team>(), out It.Ref<Formation>.IsAny),
            Times.Once);
    }

    [Fact]
    public void AcceptSample_ForAShipWithNoWaitingRecord_RejectsItAsUnknown()
    {
        var harness = new Harness(committed: true);
        var shipId = Guid.NewGuid();

        harness.Replicator.AcceptSample(new NetworkBattleShipSample(shipId, Peer, 1,
            NetworkBattleShipSample.FromFrame(MatrixFrame.Identity)));

        var inspected = JObject.FromObject(harness.Replicator.Inspect());
        Assert.Equal(0, (int)inspected["heldSamples"]!);
        harness.Engine.Verify(e => e.ApplyInput(It.IsAny<MissionObject>(), It.IsAny<BattleShipInput>()), Times.Never);
    }

    [Fact]
    public void Dispose_DropsTheRecordsAndStateHeldForDeployment()
    {
        using var mission = new MissionCurrentScope();
        var harness = new Harness(committed: true);
        harness.Engine.SetupGet(e => e.IsDeploymentMode).Returns(true);
        harness.SetupForeignSpawn();
        var record = ForeignRecord(Guid.NewGuid());
        harness.Replicator.AcceptSpawnRecords(new[] { record });
        harness.Replicator.AcceptSample(new NetworkBattleShipSample(record.ShipId, Peer, 1,
            NetworkBattleShipSample.FromFrame(MatrixFrame.Identity)));

        harness.Replicator.Dispose();

        var inspected = JObject.FromObject(harness.Replicator.Inspect());
        Assert.Equal(0, (int)inspected["pendingForeignHulls"]!);
        Assert.Equal(0, (int)inspected["heldForDeployment"]!);
        Assert.Equal(0, (int)inspected["heldSamples"]!);
    }

    private static BattleShipSpawnData ForeignRecord(Guid shipId) =>
        new BattleShipSpawnData(shipId, Peer, "MapEventParty_2", false, BattleSideEnum.Defender, 0,
            NetworkBattleShipSample.FromFrame(MatrixFrame.Identity), "northern_light_ship", null, null, null, null,
            100f, 50f, 1, "");

    private static BattleShipSpawnData AiRecord(Guid shipId) =>
        new BattleShipSpawnData(shipId, Peer, "MapEventParty_9", true, BattleSideEnum.Defender, 0,
            NetworkBattleShipSample.FromFrame(MatrixFrame.Identity), "northern_light_ship", null, null, null, null,
            100f, 50f, 1, "");

    private static float[] MovedFrame()
    {
        var frame = MatrixFrame.Identity;
        frame.origin = new Vec3(5f, 0f, 0f);
        return NetworkBattleShipSample.FromFrame(frame);
    }

    private static BattleShipCondition Condition(float hitPoints) =>
        new BattleShipCondition(hitPoints, 300f, 100f, new[] { 80f, 80f, 80f, 80f, 80f, 80f }, 0);

    private static MissionObject CreateHull() => ShipTestHulls.Create();

    private sealed class Harness
    {
        public Mock<IBattleNetwork> Network { get; } = new Mock<IBattleNetwork>();
        public Mock<INavalShipEngine> Engine { get; } = new Mock<INavalShipEngine>();
        public Mock<IBattleTeamResolver> TeamResolver { get; } = new Mock<IBattleTeamResolver>();
        public Mock<IBattleDeploymentCoordinator> Deployment { get; } = new Mock<IBattleDeploymentCoordinator>();
        public Mock<IAgentStationUseReplicator> Stations { get; } = new Mock<IAgentStationUseReplicator>();
        public Mock<IBattleSession> Session { get; } = new Mock<IBattleSession>();
        public Mock<IBattlePartyWithdrawal> PartyWithdrawal { get; } = new Mock<IBattlePartyWithdrawal>();
        /// <summary>The server-announced mission members the membership gate reads.</summary>
        public List<string> Members { get; } = new List<string>(InMission);
        public MessageBroker Broker { get; } = new MessageBroker();
        public NetworkShipRegistry Registry { get; } = new NetworkShipRegistry();
        public MissionObject OwnHull { get; } = CreateHull();
        public List<MissionObject> LiveHulls { get; } = new List<MissionObject>();
        public Guid OwnShipId { get; } = Guid.NewGuid();
        public BattleShipReplicator Replicator { get; }
        // Applies at once unless a test queues them: these tests drive the replicator alone, without the services tick.
        public IBattleShipArrivals Arrivals { get; }
        /// <summary>The host epoch the session reads; a test moves it as a host change lands.</summary>
        public int SessionEpoch { get; set; } = HostEpoch;

        public Harness(bool committed, bool resultHeld = false, int startEpoch = HostEpoch, bool queuedArrivals = false)
        {
            SessionEpoch = startEpoch;
            Arrivals = queuedArrivals ? new BattleShipArrivals() : new ImmediateShipArrivals();
            var hull = OwnHull;
            Registry.TryRegister(new NetworkShipInfo(OwnShipId, Own, "MapEventParty_1", false, hull, null));

            var component = new Mock<ICoopMissionComponent>();
            component.SetupGet(c => c.ShipRegistry).Returns(Registry);
            component.SetupGet(c => c.AgentMovementHandler).Returns(Mock.Of<IAgentMovementHandler>());

            var session = Session;
            session.SetupGet(s => s.OwnControllerId).Returns(Own);
            session.SetupGet(s => s.HostEpoch).Returns(() => SessionEpoch);
            session.SetupGet(s => s.InstanceId).Returns(Instance);
            session.Setup(s => s.IsOwn(It.IsAny<string>())).Returns((string id) => id == Own);
            session.SetupGet(s => s.IsNaval).Returns(true);

            var deployment = Deployment;
            deployment.SetupGet(d => d.IsCommitted).Returns(committed);

            var engine = Engine;
            LiveHulls.Add(hull);
            engine.SetupGet(e => e.Hulls).Returns(() => LiveHulls);
            engine.Setup(e => e.AgentsOnDeck(It.IsAny<MissionObject>())).Returns(Array.Empty<Agent>());
            engine.Setup(e => e.ParkHullAgents(It.IsAny<MissionObject>())).Returns(Array.Empty<HullSwapAgent>());
            engine.Setup(e => e.GetFrame(It.IsAny<MissionObject>())).Returns(MatrixFrame.Identity);
            engine.Setup(e => e.Describe(It.IsAny<MissionObject>(), It.IsAny<NetworkShipInfo>())).Returns((MissionObject _, NetworkShipInfo info) =>
                new BattleShipSpawnData(info.ShipId, info.CurrentAuthority, info.MapEventPartyId, false, BattleSideEnum.Defender,
                    0, NetworkBattleShipSample.FromFrame(MatrixFrame.Identity), "northern_light_ship", null, null, null, null,
                    100f, 50f, 1, ""));

            var resultCommitter = new Mock<IBattleResultCommitter>();
            var resolvedState = BattleState.DefenderVictory;
            resultCommitter.Setup(c => c.TryGetResolvedState(out resolvedState)).Returns(resultHeld);

            var epochs = new HostEpochPolicy();
            var members = new Mock<IMissionContext>();
            members.SetupGet(m => m.ControllersInMission).Returns(() => Members.ToArray());
            var context = new BattleContext(session.Object, deployment.Object, component.Object, epochs, resultCommitter.Object,
                Mock.Of<IReinforcementFielder>(), members.Object, Mock.Of<IBattleTroopWithdrawal>(), PartyWithdrawal.Object);
            Replicator = new BattleShipReplicator(Network.Object, Broker, context, engine.Object, TeamResolver.Object,
                Mock.Of<IObjectManager>(), new NavalHullAuthority(session.Object, component.Object, engine.Object, context.TroopWithdrawal),
                new BattleShipConditionStream(Network.Object, session.Object, engine.Object, epochs, members.Object), Stations.Object,
                Mock.Of<INavalShipCaptureEngine>(), Arrivals);
        }

        // A foreign record resolves a team and spawns one live copy hull.
        public MissionObject SetupForeignSpawn(bool initialShipsDeployed = true)
        {
            var team = (Team)RuntimeHelpers.GetUninitializedObject(typeof(Team));
            TeamResolver.Setup(r => r.ResolveReplicatedTeam(It.IsAny<BattleSideEnum>(), false)).Returns(team);
            if (initialShipsDeployed) Deployment.SetupGet(d => d.IsTeamSetupOver).Returns(true);

            var copy = CreateHull();
            Formation formation = null!;
            Engine.Setup(e => e.SpawnForeignHull(It.IsAny<BattleShipSpawnData>(), team, out formation))
                .Returns(() =>
                {
                    LiveHulls.Add(copy);
                    return copy;
                });
            return copy;
        }
    }
}
