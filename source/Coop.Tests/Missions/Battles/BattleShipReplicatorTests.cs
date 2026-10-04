using Common.Messaging;
using GameInterface.Services.ObjectManager;
using Missions;
using Missions.Agents.Handlers;
using Missions.Battles;
using Missions.Messages;
using Moq;
using ProtoBuf;
using System;
using System.IO;
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

        Assert.Equal(expected, BattleShipReplicator.ValidateSample(ship, Own, sample, accepted, now));
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

    private static MissionObject CreateHull() => ShipTestHulls.Create();

    private sealed class Harness
    {
        public Mock<IBattleNetwork> Network { get; } = new Mock<IBattleNetwork>();
        public BattleShipReplicator Replicator { get; }

        public Harness(bool committed)
        {
            var hull = CreateHull();
            var registry = new NetworkShipRegistry();
            registry.TryRegister(new NetworkShipInfo(Guid.NewGuid(), Own, "MapEventParty_1", false, hull, null));

            var component = new Mock<ICoopMissionComponent>();
            component.SetupGet(c => c.ShipRegistry).Returns(registry);
            component.SetupGet(c => c.AgentMovementHandler).Returns(Mock.Of<IAgentMovementHandler>());

            var session = new Mock<IBattleSession>();
            session.SetupGet(s => s.OwnControllerId).Returns(Own);
            session.Setup(s => s.IsOwn(It.IsAny<string>())).Returns((string id) => id == Own);

            var deployment = new Mock<IBattleDeploymentCoordinator>();
            deployment.SetupGet(d => d.IsCommitted).Returns(committed);

            var engine = new Mock<INavalShipEngine>();
            engine.SetupGet(e => e.Hulls).Returns(new[] { hull });
            engine.Setup(e => e.GetFrame(hull)).Returns(MatrixFrame.Identity);
            engine.Setup(e => e.Describe(hull, It.IsAny<NetworkShipInfo>())).Returns((MissionObject _, NetworkShipInfo info) =>
                new BattleShipSpawnData(info.ShipId, info.CurrentAuthority, info.MapEventPartyId, false, BattleSideEnum.Defender,
                    0, NetworkBattleShipSample.FromFrame(MatrixFrame.Identity), "northern_light_ship", null, null, null, null,
                    100f, 50f, 1, ""));

            Replicator = new BattleShipReplicator(Network.Object, new MessageBroker(), session.Object, deployment.Object,
                component.Object, engine.Object, Mock.Of<IBattleTeamResolver>(), Mock.Of<IObjectManager>());
        }
    }
}
