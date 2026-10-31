using Common.Messaging;
using Common;
using Missions;
using Missions.Battles;
using Missions.Messages;
using Moq;
using Newtonsoft.Json.Linq;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

[Collection("Mission.Current")]
public class BattleShipDamageRouterTests
{
    private const string Own = "local";
    private const string Peer = "peer";
    private const string Successor = "successor";
    private const int HostEpoch = 3;

    [Fact]
    public void ForeignHullHit_IsSentOnlyToTheHullsAuthority()
    {
        var harness = new Harness();
        var hull = harness.Register(Peer, isNpcParty: false);

        harness.Broker.Publish(this, Hit(hull.Hull, BattleShipDamageKind.Fire, 30f));

        harness.Network.Verify(n => n.Send(Peer, It.Is<IMessage>(m => m is NetworkApplyShipDamage
            && ((NetworkApplyShipDamage)m).ShipId == hull.ShipId
            && ((NetworkApplyShipDamage)m).Kind == BattleShipDamageKind.Fire
            && ((NetworkApplyShipDamage)m).Damage == 30f
            && ((NetworkApplyShipDamage)m).SenderControllerId == Own)), Times.Once);
        harness.Network.Verify(n => n.SendAll(It.IsAny<IMessage>()), Times.Never);
        harness.Engine.Verify(e => e.ApplyShipDamage(It.IsAny<MissionObject>(), It.IsAny<NetworkApplyShipDamage>(),
            It.IsAny<Agent>(), It.IsAny<MissionObject>()), Times.Never);
        Assert.Equal(1, (long)harness.Inspect()["sent"]!);
    }

    [Fact]
    public void HitOnAHullThisClientAlreadyOwns_IsAppliedHereInsteadOfSent()
    {
        var harness = new Harness();
        var hull = harness.Register(Own, isNpcParty: true);

        harness.Broker.Publish(this, Hit(hull.Hull, BattleShipDamageKind.Hull, 50f));

        harness.Engine.Verify(e => e.ApplyShipDamage(hull.Hull, It.Is<NetworkApplyShipDamage>(d => d.Damage == 50f), null, null),
            Times.Once);
        harness.Network.Verify(n => n.Send(It.IsAny<string>(), It.IsAny<IMessage>()), Times.Never);
    }

    [Fact]
    public void HitOnAnUnregisteredHull_IsDropped()
    {
        var harness = new Harness();

        harness.Broker.Publish(this, Hit(ShipTestHulls.Create(), BattleShipDamageKind.Hull, 50f));

        harness.Network.Verify(n => n.Send(It.IsAny<string>(), It.IsAny<IMessage>()), Times.Never);
        Assert.Equal(1, harness.Drops("unregistered_hull"));
    }

    [Fact]
    public void RoutedHit_IsAppliedByTheHullsAuthorityWithTheResolvedHitter()
    {
        var harness = new Harness();
        var target = harness.Register(Own, isNpcParty: false);
        var rammer = harness.Register(Peer, isNpcParty: false);
        var damage = Damage(target.ShipId, BattleShipDamageKind.Collision, 120f, hitterShipId: rammer.ShipId);

        harness.Router.ApplyNetworkDamage(damage);

        harness.Engine.Verify(e => e.ApplyShipDamage(target.Hull, damage, null, rammer.Hull), Times.Once);
        Assert.Equal(1, (long)harness.Inspect()["applied"]!);
    }

    [Fact]
    public void RoutedHit_OnAHullAnotherClientSimulates_IsDropped()
    {
        var harness = new Harness();
        var hull = harness.Register(Peer, isNpcParty: false);

        harness.Router.ApplyNetworkDamage(Damage(hull.ShipId, BattleShipDamageKind.Hull, 10f));

        harness.Engine.Verify(e => e.ApplyShipDamage(It.IsAny<MissionObject>(), It.IsAny<NetworkApplyShipDamage>(),
            It.IsAny<Agent>(), It.IsAny<MissionObject>()), Times.Never);
        Assert.Equal(1, harness.Drops("not_authority"));
    }

    [Fact]
    public void RoutedHit_OnAnUnknownHull_IsDropped()
    {
        var harness = new Harness();

        harness.Router.ApplyNetworkDamage(Damage(Guid.NewGuid(), BattleShipDamageKind.Hull, 10f));

        Assert.Equal(1, harness.Drops("unknown_ship"));
    }

    [Theory]
    [InlineData(Own, true, 10f, null)]
    [InlineData(Own, false, 10f, null)]
    [InlineData(Peer, true, 10f, "not_authority")]
    [InlineData(Peer, false, 10f, "not_authority")]
    [InlineData(Own, false, float.NaN, "invalid_damage")]
    [InlineData(Own, false, -1f, "invalid_damage")]
    public void ValidateDamage_AppliesOnlyAtTheHullsCurrentAuthority(string authority, bool isNpcParty, float amount, string expected)
    {
        var ship = new NetworkShipInfo(Guid.NewGuid(), authority, "MapEventParty_9", isNpcParty, ShipTestHulls.Create(), null);

        Assert.Equal(expected, BattleShipDamageRouter.ValidateDamage(ship, Own, Damage(ship.ShipId, BattleShipDamageKind.Hull, amount)));
    }

    [Fact]
    public void Dispose_StopsRoutingHits()
    {
        var harness = new Harness();
        var hull = harness.Register(Peer, isNpcParty: false);

        harness.Router.Dispose();
        harness.Broker.Publish(this, Hit(hull.Hull, BattleShipDamageKind.Hull, 5f));

        harness.Network.Verify(n => n.Send(It.IsAny<string>(), It.IsAny<IMessage>()), Times.Never);
    }

    [Fact]
    public void NetworkApplyShipDamage_RoundTripsEveryField()
    {
        var damage = new NetworkApplyShipDamage(Guid.NewGuid(), Peer, BattleShipDamageKind.Collision, 120.5f, 7.25f,
            new Vec3(1f, -2f, 3.5f), Guid.NewGuid(), Guid.NewGuid(), isRamDamage: true, sailIndex: 2);

        using var stream = new MemoryStream();
        Serializer.Serialize(stream, damage);
        stream.Position = 0;
        var copy = Serializer.Deserialize<NetworkApplyShipDamage>(stream);

        Assert.Equal(damage.ShipId, copy.ShipId);
        Assert.Equal(damage.SenderControllerId, copy.SenderControllerId);
        Assert.Equal(damage.Kind, copy.Kind);
        Assert.Equal(damage.Damage, copy.Damage);
        Assert.Equal(damage.InflictedDamage, copy.InflictedDamage);
        Assert.Equal(1f, copy.LocalPoint.x);
        Assert.Equal(-2f, copy.LocalPoint.y);
        Assert.Equal(3.5f, copy.LocalPoint.z);
        Assert.Equal(damage.AttackerAgentId, copy.AttackerAgentId);
        Assert.Equal(damage.HitterShipId, copy.HitterShipId);
        Assert.True(copy.IsRamDamage);
        Assert.Equal(2, copy.SailIndex);
    }

    // A hit routed to the new AI host can arrive after the host change reached this client but before the change's queued
    // hull move applied; it waits in the arrivals queue behind that move and applies once the hull is this client's.
    [Fact]
    public void RoutedHit_ArrivingBeforeTheQueuedHostChange_AppliesOnceTheHullMoves()
    {
        using var mission = new MissionCurrentScope();
        var harness = new Harness(sessionEpoch: HostEpoch + 1);
        var ship = harness.Register(Peer, isNpcParty: true);
        harness.QueueHostChange(ship, Own);
        var damage = Damage(ship.ShipId, BattleShipDamageKind.Hull, 10f);

        harness.Broker.Publish(this, damage);
        GameThread.Run(() => { }, blocking: true);
        harness.Engine.Verify(e => e.ApplyShipDamage(It.IsAny<MissionObject>(), It.IsAny<NetworkApplyShipDamage>(),
            It.IsAny<Agent>(), It.IsAny<MissionObject>()), Times.Never);
        harness.Arrivals.ApplyAll();

        harness.Engine.Verify(e => e.ApplyShipDamage(ship.Hull, damage, null, null), Times.Once);
    }

    // Q2: the hit goes only to the authority in the sender's view, so a capture that moves the hull between the old
    // authority's apply and the captor's can't apply it a second time (a broadcast reaches both).
    [Fact]
    public void HitAcrossACapture_IsAppliedAtMostOnce()
    {
        const string oldAuthority = "old-authority", captor = "captor";
        var sender = new Harness();
        var peers = new Dictionary<string, Harness>
        {
            [oldAuthority] = new Harness(own: oldAuthority),
            [captor] = new Harness(own: captor),
        };
        var shipId = Guid.NewGuid();
        var onSender = sender.Register(oldAuthority, isNpcParty: false, shipId);
        peers[oldAuthority].Register(oldAuthority, isNpcParty: false, shipId);
        // The captor already applied its capture; the old authority applies the hit before its own.
        peers[captor].Register(captor, isNpcParty: false, shipId);
        var delivered = new List<(string To, NetworkApplyShipDamage Damage)>();
        sender.Network.Setup(n => n.Send(It.IsAny<string>(), It.IsAny<IMessage>()))
            .Callback<string, IMessage>((to, message) => delivered.Add((to, (NetworkApplyShipDamage)message)));
        sender.Network.Setup(n => n.SendAll(It.IsAny<IMessage>()))
            .Callback<IMessage>(message =>
            {
                foreach (var peer in peers.Keys) delivered.Add((peer, (NetworkApplyShipDamage)message));
            });

        sender.Broker.Publish(this, Hit(onSender.Hull, BattleShipDamageKind.Hull, 30f));
        foreach (var (to, damage) in delivered) peers[to].Router.ApplyNetworkDamage(damage);

        Assert.Equal(1, (long)peers[oldAuthority].Inspect()["applied"]! + (long)peers[captor].Inspect()["applied"]!);
    }

    // S14 A4: the sender's hit on an AI hull waits behind a host change the session already read, then goes to the successor.
    [Fact]
    public void LocalHit_OnAnAiHullWithAQueuedHostChange_IsSentToTheSuccessorOnceTheMoveApplies()
    {
        var harness = new Harness(sessionEpoch: HostEpoch + 1);
        var ship = harness.Register(Peer, isNpcParty: true);
        harness.QueueHostChange(ship, Successor);

        harness.Broker.Publish(this, Hit(ship.Hull, BattleShipDamageKind.Hull, 10f));
        harness.Network.Verify(n => n.Send(It.IsAny<string>(), It.IsAny<IMessage>()), Times.Never);
        harness.Arrivals.ApplyAll();

        harness.Network.Verify(n => n.Send(Successor, It.Is<IMessage>(m => ((NetworkApplyShipDamage)m).ShipId == ship.ShipId)),
            Times.Once);
        harness.Network.Verify(n => n.Send(Peer, It.IsAny<IMessage>()), Times.Never);
        Assert.Equal(1, (long)harness.Inspect()["deferred"]!);
    }

    // S14 A4: when this client is the successor, the held hit lands here once the queued move made the hull its own.
    [Fact]
    public void LocalHit_OnAnAiHullThisClientIsTakingOver_IsAppliedHereOnceTheMoveApplies()
    {
        var harness = new Harness(sessionEpoch: HostEpoch + 1);
        var ship = harness.Register(Peer, isNpcParty: true);
        harness.QueueHostChange(ship, Own);

        harness.Broker.Publish(this, Hit(ship.Hull, BattleShipDamageKind.Hull, 10f));
        harness.Arrivals.ApplyAll();

        harness.Engine.Verify(e => e.ApplyShipDamage(ship.Hull, It.Is<NetworkApplyShipDamage>(d => d.Damage == 10f), null, null),
            Times.Once);
        harness.Network.Verify(n => n.Send(It.IsAny<string>(), It.IsAny<IMessage>()), Times.Never);
    }

    // S14 A4: the hold is taken once, so a hit is still sent when the applied epoch keeps lagging the session's.
    [Fact]
    public void DeferredHit_IsSentAtTheNextTick_EvenWhenTheAppliedEpochStillLags()
    {
        var harness = new Harness(sessionEpoch: HostEpoch + 1);
        var ship = harness.Register(Peer, isNpcParty: true);

        harness.Broker.Publish(this, Hit(ship.Hull, BattleShipDamageKind.Hull, 10f));
        harness.Arrivals.ApplyAll();

        harness.Network.Verify(n => n.Send(Peer, It.IsAny<IMessage>()), Times.Once);
        Assert.Equal(0, harness.Arrivals.Count);
    }

    // A player hull doesn't follow the battle host, so a queued host change doesn't hold its hits.
    [Fact]
    public void LocalHit_OnAPlayerHull_IsSentAtOnceWhileAHostChangeIsQueued()
    {
        var harness = new Harness(sessionEpoch: HostEpoch + 1);
        var ship = harness.Register(Peer, isNpcParty: false);

        harness.Broker.Publish(this, Hit(ship.Hull, BattleShipDamageKind.Hull, 10f));

        harness.Network.Verify(n => n.Send(Peer, It.IsAny<IMessage>()), Times.Once);
        Assert.Equal(0, (long)harness.Inspect()["deferred"]!);
    }

    private static BattleShipHit Hit(MissionObject hull, BattleShipDamageKind kind, float damage) =>
        new BattleShipHit(hull, kind, damage, 0f, Vec3.Zero, null, null, false, -1);

    private static NetworkApplyShipDamage Damage(Guid shipId, BattleShipDamageKind kind, float damage, Guid hitterShipId = default) =>
        new NetworkApplyShipDamage(shipId, Peer, kind, damage, 0f, Vec3.Zero, Guid.Empty, hitterShipId, false, -1);

    private sealed class Harness
    {
        public Mock<IBattleNetwork> Network { get; } = new Mock<IBattleNetwork>();
        public Mock<INavalShipEngine> Engine { get; } = new Mock<INavalShipEngine>();
        public MessageBroker Broker { get; } = new MessageBroker();
        public NetworkShipRegistry Registry { get; } = new NetworkShipRegistry();
        public BattleShipDamageRouter Router { get; }
        public BattleShipArrivals Arrivals { get; } = new BattleShipArrivals();
        /// <summary>The host epoch whose hull move this client applied; a test moves it as a queued host change applies.</summary>
        public int AppliedEpoch { get; set; } = HostEpoch;

        public Harness(int sessionEpoch = HostEpoch, string own = Own)
        {
            var component = new Mock<ICoopMissionComponent>();
            component.SetupGet(c => c.ShipRegistry).Returns(Registry);
            component.SetupGet(c => c.AgentRegistry).Returns(Mock.Of<INetworkAgentRegistry>());

            var session = new Mock<IBattleSession>();
            session.SetupGet(s => s.OwnControllerId).Returns(own);
            session.SetupGet(s => s.HostEpoch).Returns(sessionEpoch);
            var appliedEpoch = new Mock<IAppliedHostEpoch>();
            appliedEpoch.SetupGet(e => e.AppliedHostEpoch).Returns(() => AppliedEpoch);

            Router = new BattleShipDamageRouter(Network.Object, Broker, session.Object, component.Object, Engine.Object,
                appliedEpoch.Object, Arrivals);
        }

        public NetworkShipInfo Register(string authority, bool isNpcParty, Guid shipId = default)
        {
            var ship = new NetworkShipInfo(shipId == Guid.Empty ? Guid.NewGuid() : shipId, authority, "MapEventParty_9", isNpcParty,
                ShipTestHulls.Create(), null);
            Registry.TryRegister(ship);
            return ship;
        }

        // The hull move of a host change the session already reads, waiting in the arrivals queue as the replicator queues it.
        public void QueueHostChange(NetworkShipInfo ship, string newHost) => Arrivals.Enqueue(() =>
        {
            ship.CurrentAuthority = newHost;
            AppliedEpoch = HostEpoch + 1;
        }, "host change");

        public JObject Inspect() => JObject.FromObject(Router.Inspect());

        // How many hits were dropped for the reason.
        public long Drops(string reason) => Inspect()["drops"]![reason]?.Value<long>() ?? 0;
    }
}
