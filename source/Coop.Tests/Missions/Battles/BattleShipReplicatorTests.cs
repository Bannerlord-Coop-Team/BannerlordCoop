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
        Assert.Equal(2, (long)inspected["applied"]!);
        harness.Engine.Verify(e => e.ApplyForeignFrame(copy, It.IsAny<MatrixFrame>()), Times.Exactly(2));
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
        harness.Broker.Publish(this, new MissionPeerDisconnected(Peer, Instance));
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

    // R7: a record a player sent before it left can arrive after its departure; it never spawns a copy here.
    [Fact]
    public void AcceptSpawnRecords_FromAPlayerNoLongerInTheMission_SpawnsNothing()
    {
        using var mission = new MissionCurrentScope();
        var harness = new Harness(committed: false);
        harness.SetupForeignSpawn();
        var record = ForeignRecord(Guid.NewGuid());

        harness.Members.Remove(Peer);
        harness.Replicator.AcceptSpawnRecords(new[] { record });

        Assert.Equal(ShipLifecycle.Unknown, harness.Registry.StateOf(record.ShipId));
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

        harness.Replicator.TransferNpcHulls("third_peer");

        Assert.Equal(12, beforeMove);
        Assert.Equal(0, harness.Replicator.ReportedReserveOf(copy));
    }

    // A new owner of an AI hull reports its own reserve at its first check, even when it equals the last one reported.
    [Fact]
    public void TransferNpcHulls_ToThisClient_SendsTheReserveAgainAtTheFirstCheck()
    {
        var harness = new Harness(committed: true);
        var (_, ship) = RegisterPeerCopy(harness, isNpcParty: true);
        harness.Replicator.AcceptCondition(new NetworkShipCondition(ship.ShipId, Peer, 1, HostEpoch, Condition(100f), 12));
        harness.Engine.Setup(e => e.ReadCondition(It.IsAny<MissionObject>())).Returns(Condition(100f));
        harness.Engine.Setup(e => e.ReadSpawnableReserve(It.IsAny<MissionObject>())).Returns(12);

        harness.Replicator.TransferNpcHulls(Own);
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

        harness.Replicator.AcceptRemoval(new NetworkShipRemoved(ship.ShipId, Peer, ShipRemovalReason.Retreat));
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

        harness.Replicator.AcceptRemoval(new NetworkShipRemoved(ship.ShipId, "someone_else", ShipRemovalReason.Leave));
        harness.Replicator.AcceptRemoval(new NetworkShipRemoved(harness.OwnShipId, Own, ShipRemovalReason.Leave));

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

        harness.Replicator.AcceptRemoval(new NetworkShipRemoved(ship.ShipId, Peer, ShipRemovalReason.Retreat));

        harness.Engine.Verify(e => e.MoveAgentToHull(ally, harness.OwnHull), Times.Once);
        harness.Engine.Verify(e => e.MoveAgentToHull(stray, nearest), Times.Once);
        harness.Engine.Verify(e => e.MoveAgentToHull(crew, It.IsAny<MissionObject>()), Times.Never);
        Assert.Equal(2, (long)JObject.FromObject(harness.Replicator.Inspect())["agentsMovedOffDepartedHulls"]!);
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
    public void TransferNpcHulls_ToThisClient_TakesOverOnlyTheAiHulls()
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

        Assert.Equal(1, harness.Replicator.TransferNpcHulls(Own));
        Assert.Equal(0, harness.Replicator.TransferNpcHulls(Own));
        harness.Replicator.Tick(0.1f);

        Assert.Equal(Own, ai.CurrentAuthority);
        Assert.Equal(Peer, peerShip.CurrentAuthority);
        harness.Engine.Verify(e => e.ParkHullAgents(aiHull), Times.Once);
        harness.Engine.Verify(e => e.ParkHullAgents(peerHull), Times.Never);
    }

    [Fact]
    public void TransferNpcHulls_ToThisClient_ChangesNoHullBeforeTheNextMissionTick()
    {
        var harness = new Harness(committed: true);
        var copy = CreateHull();
        harness.Registry.TryRegister(new NetworkShipInfo(Guid.NewGuid(), Peer, "MapEventParty_9", true, copy, null));

        harness.Replicator.TransferNpcHulls(Own);

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
        harness.Replicator.TransferNpcHulls(Own);

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
        harness.Replicator.TransferNpcHulls(Own);

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
        harness.Replicator.TransferNpcHulls(Own);

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
        harness.Replicator.TransferNpcHulls(Own);

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
        harness.Replicator.TransferNpcHulls(Own);

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
    public void TransferNpcHulls_AwayMidSwap_BoardsTheParkedAgentsAndStopsTheSwap()
    {
        var harness = new Harness(committed: true);
        var copy = CreateHull();
        harness.Registry.TryRegister(new NetworkShipInfo(Guid.NewGuid(), Peer, "MapEventParty_9", true, copy, null));
        harness.LiveHulls.Add(copy);
        var parked = new[] { new HullSwapAgent(null, isCrew: true, isParked: true, Vec3.Zero, Vec3.Zero) };
        harness.Engine.Setup(e => e.ParkHullAgents(copy)).Returns(parked);
        harness.Replicator.TransferNpcHulls(Own);
        harness.Replicator.Tick(0.1f);

        harness.Replicator.TransferNpcHulls(Peer);
        harness.Replicator.Tick(0.1f);

        harness.Engine.Verify(e => e.BoardHullAgents(copy, parked), Times.Once);
        harness.Engine.Verify(e => e.ReleaseNpcHull(copy), Times.Once);
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
    public void TransferNpcHulls_AwayFromThisClient_HandsTheHullBackToSamples()
    {
        var harness = new Harness(committed: true);
        var aiHull = CreateHull();
        var ai = new NetworkShipInfo(Guid.NewGuid(), Own, "MapEventParty_9", true, aiHull, null);
        harness.Registry.TryRegister(ai);

        harness.Replicator.TransferNpcHulls(Peer);

        Assert.Equal(Peer, ai.CurrentAuthority);
        harness.Engine.Verify(e => e.ReleaseNpcHull(aiHull), Times.Once);
    }

    [Fact]
    public void TransferNpcHulls_BetweenTwoPeers_LeavesTheLocalCopyKinematic()
    {
        var harness = new Harness(committed: true);
        var aiHull = CreateHull();
        var ai = new NetworkShipInfo(Guid.NewGuid(), Peer, "MapEventParty_9", true, aiHull, null);
        harness.Registry.TryRegister(ai);
        harness.LiveHulls.Add(aiHull);

        harness.Replicator.TransferNpcHulls("successor");
        harness.Replicator.Tick(0.1f);

        Assert.Equal("successor", ai.CurrentAuthority);
        harness.Engine.Verify(e => e.ReleaseNpcHull(It.IsAny<MissionObject>()), Times.Never);
        harness.Engine.Verify(e => e.ParkHullAgents(It.IsAny<MissionObject>()), Times.Never);
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
        harness.Engine.Verify(e => e.ApplyForeignFrame(copy, It.Is<MatrixFrame>(f => f.origin.x == 5f)), Times.Once);
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
        harness.Replicator.TransferNpcHulls("successor");
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
        /// <summary>The server-announced mission members the membership gate reads.</summary>
        public List<string> Members { get; } = new List<string>(InMission);
        public MessageBroker Broker { get; } = new MessageBroker();
        public NetworkShipRegistry Registry { get; } = new NetworkShipRegistry();
        public MissionObject OwnHull { get; } = CreateHull();
        public List<MissionObject> LiveHulls { get; } = new List<MissionObject>();
        public Guid OwnShipId { get; } = Guid.NewGuid();
        public BattleShipReplicator Replicator { get; }

        public Harness(bool committed, bool resultHeld = false)
        {
            var hull = OwnHull;
            Registry.TryRegister(new NetworkShipInfo(OwnShipId, Own, "MapEventParty_1", false, hull, null));

            var component = new Mock<ICoopMissionComponent>();
            component.SetupGet(c => c.ShipRegistry).Returns(Registry);
            component.SetupGet(c => c.AgentMovementHandler).Returns(Mock.Of<IAgentMovementHandler>());

            var session = new Mock<IBattleSession>();
            session.SetupGet(s => s.OwnControllerId).Returns(Own);
            session.SetupGet(s => s.HostEpoch).Returns(HostEpoch);
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
                Mock.Of<IReinforcementFielder>(), members.Object);
            Replicator = new BattleShipReplicator(Network.Object, Broker, context, engine.Object, TeamResolver.Object,
                Mock.Of<IObjectManager>(), new NavalHullAuthority(session.Object, component.Object, engine.Object),
                new BattleShipConditionStream(Network.Object, session.Object, engine.Object, epochs, members.Object), Stations.Object);
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
