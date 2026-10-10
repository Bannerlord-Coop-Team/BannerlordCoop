using Common;
using Common.Messaging;
using GameInterface.Services.ObjectManager;
using Missions;
using Missions.Agents.Handlers;
using Missions.Battles;
using Missions.Messages;
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

public class BattleShipReplicatorTests
{
    private const string Own = "local";
    private const string Peer = "peer";
    private const string Instance = "MapEvent_1";
    private const int HostEpoch = 3;

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

    [Theory]
    [InlineData(Peer, Peer, 5, 0, 500, null)]
    [InlineData(Peer, "someone_else", 5, 0, 500, "not_authority")]
    [InlineData(Peer, Peer, 5, 5, 500, "stale")]
    [InlineData(Peer, Peer, 5, 0, -1, "expired")]
    [InlineData(Peer, Peer, 5, 0, 5000, "expired")]
    [InlineData(Own, Own, 5, 0, 500, "own_hull")]
    public void ValidateSample_AcceptsOnlyFreshSamplesFromTheHullAuthority(
        string authority, string sender, long sequence, long accepted, int deadlineOffsetMs, string expected)
    {
        var ship = new NetworkShipInfo(Guid.NewGuid(), authority, null, false, CreateHull(), null);
        long now = DateTime.UtcNow.Ticks;
        var sample = new NetworkBattleShipSample(ship.ShipId, sender, sequence,
            now + TimeSpan.FromMilliseconds(deadlineOffsetMs).Ticks, NetworkBattleShipSample.FromFrame(MatrixFrame.Identity));

        Assert.Equal(expected, BattleShipReplicator.ValidateSample(ship, Own, sample, accepted, 0, now));
    }

    [Theory]
    [InlineData(2, 10, 3, 4, "stale_epoch")]
    [InlineData(3, 3, 3, 4, "stale")]
    [InlineData(4, 1, 3, 10, null)]
    [InlineData(3, 11, 3, 10, null)]
    public void ValidateSample_AiHull_RestartsTheSequenceOnlyForANewerHostEpoch(
        int sampleEpoch, long sequence, int acceptedEpoch, long acceptedSequence, string expected)
    {
        var ship = new NetworkShipInfo(Guid.NewGuid(), Peer, "MapEventParty_9", true, CreateHull(), null);
        long now = DateTime.UtcNow.Ticks;
        var sample = new NetworkBattleShipSample(ship.ShipId, Peer, sequence, now + TimeSpan.FromMilliseconds(500).Ticks,
            NetworkBattleShipSample.FromFrame(MatrixFrame.Identity), default, null, sampleEpoch);

        Assert.Equal(expected, BattleShipReplicator.ValidateSample(ship, Own, sample, acceptedSequence, acceptedEpoch, now));
    }

    [Fact]
    public void ValidateSample_PlayerHull_IgnoresTheHostEpoch()
    {
        var ship = new NetworkShipInfo(Guid.NewGuid(), Peer, "MapEventParty_2", false, CreateHull(), null);
        long now = DateTime.UtcNow.Ticks;
        var older = new NetworkBattleShipSample(ship.ShipId, Peer, 11, now + TimeSpan.FromMilliseconds(500).Ticks,
            NetworkBattleShipSample.FromFrame(MatrixFrame.Identity), default, null, 1);
        var newerButStale = new NetworkBattleShipSample(ship.ShipId, Peer, 5, now + TimeSpan.FromMilliseconds(500).Ticks,
            NetworkBattleShipSample.FromFrame(MatrixFrame.Identity), default, null, 9);

        Assert.Null(BattleShipReplicator.ValidateSample(ship, Own, older, 10, 5, now));
        Assert.Equal("stale", BattleShipReplicator.ValidateSample(ship, Own, newerButStale, 10, 5, now));
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
        var parked = new[] { new HullSwapAgent(null, isCrew: true, isParked: true, Vec3.Zero) };
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
        var parked = new[] { new HullSwapAgent(null, isCrew: true, isParked: true, Vec3.Zero) };
        harness.Engine.Setup(e => e.ParkHullAgents(copy)).Returns(parked);
        harness.Replicator.TransferNpcHulls(Own);

        harness.Replicator.Tick(0.1f);
        harness.Replicator.Tick(0.1f);
        harness.Replicator.Tick(0.1f);

        harness.Engine.Verify(e => e.BoardHullAgents(copy, parked), Times.Once);
        Assert.Same(copy, ai.Hull);
    }

    [Fact]
    public void TransferNpcHulls_AwayMidSwap_BoardsTheParkedAgentsAndStopsTheSwap()
    {
        var harness = new Harness(committed: true);
        var copy = CreateHull();
        harness.Registry.TryRegister(new NetworkShipInfo(Guid.NewGuid(), Peer, "MapEventParty_9", true, copy, null));
        harness.LiveHulls.Add(copy);
        var parked = new[] { new HullSwapAgent(null, isCrew: true, isParked: true, Vec3.Zero) };
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
        long now = DateTime.UtcNow.Ticks;
        var sample = new NetworkBattleShipSample(ship.ShipId, Peer, 1, now + TimeSpan.FromMilliseconds(500).Ticks,
            NetworkBattleShipSample.FromFrame(MatrixFrame.Identity), new BattleShipInput(0, 1, 0, float.NaN, 2));

        Assert.Equal("invalid_input", BattleShipReplicator.ValidateSample(ship, Own, sample, 0, 0, now));
    }

    [Fact]
    public void ValidateSample_RejectsAnInvalidRopeSet()
    {
        var ship = new NetworkShipInfo(Guid.NewGuid(), Peer, null, false, CreateHull(), null);
        long now = DateTime.UtcNow.Ticks;
        var ropes = new[] { BattleRopeStateTests.Rope(1, BattleRopeState.RopesPulling), BattleRopeStateTests.Rope(2, BattleRopeState.Removed) };
        var sample = new NetworkBattleShipSample(ship.ShipId, Peer, 1, now + TimeSpan.FromMilliseconds(500).Ticks,
            NetworkBattleShipSample.FromFrame(MatrixFrame.Identity), default, ropes);

        Assert.Equal("invalid_ropes", BattleShipReplicator.ValidateSample(ship, Own, sample, 0, 0, now));
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

        Assert.Equal(expected, BattleShipReplicator.ValidateFinalRopes(ship, Own, final));
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
        var sample = new NetworkBattleShipSample(Guid.NewGuid(), Peer, 7, 1234,
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

        Assert.Equal(expected, BattleShipReplicator.ValidateCondition(ship, Own, condition, acceptedRevision, acceptedEpoch));
    }

    [Fact]
    public void ValidateCondition_RejectsANonFiniteCondition()
    {
        var ship = new NetworkShipInfo(Guid.NewGuid(), Peer, null, false, CreateHull(), null);
        var condition = new NetworkShipCondition(ship.ShipId, Peer, 1, 0, Condition(float.NaN));

        Assert.Equal("invalid_condition", BattleShipReplicator.ValidateCondition(ship, Own, condition, 0, 0));
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

    private static BattleShipCondition Condition(float hitPoints) =>
        new BattleShipCondition(hitPoints, 300f, 100f, new[] { 80f, 80f, 80f, 80f, 80f, 80f }, 0);

    private static MissionObject CreateHull() => ShipTestHulls.Create();

    private sealed class Harness
    {
        public Mock<IBattleNetwork> Network { get; } = new Mock<IBattleNetwork>();
        public Mock<INavalShipEngine> Engine { get; } = new Mock<INavalShipEngine>();
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

            var deployment = new Mock<IBattleDeploymentCoordinator>();
            deployment.SetupGet(d => d.IsCommitted).Returns(committed);

            var engine = Engine;
            LiveHulls.Add(hull);
            engine.SetupGet(e => e.Hulls).Returns(() => LiveHulls);
            engine.Setup(e => e.GetFrame(It.IsAny<MissionObject>())).Returns(MatrixFrame.Identity);
            engine.Setup(e => e.Describe(It.IsAny<MissionObject>(), It.IsAny<NetworkShipInfo>())).Returns((MissionObject _, NetworkShipInfo info) =>
                new BattleShipSpawnData(info.ShipId, info.CurrentAuthority, info.MapEventPartyId, false, BattleSideEnum.Defender,
                    0, NetworkBattleShipSample.FromFrame(MatrixFrame.Identity), "northern_light_ship", null, null, null, null,
                    100f, 50f, 1, ""));

            var resultCommitter = new Mock<IBattleResultCommitter>();
            var resolvedState = BattleState.DefenderVictory;
            resultCommitter.Setup(c => c.TryGetResolvedState(out resolvedState)).Returns(resultHeld);

            Replicator = new BattleShipReplicator(Network.Object, Broker, session.Object, deployment.Object,
                component.Object, engine.Object, Mock.Of<IBattleTeamResolver>(), Mock.Of<IObjectManager>(), new HostEpochPolicy(),
                resultCommitter.Object);
        }
    }
}
