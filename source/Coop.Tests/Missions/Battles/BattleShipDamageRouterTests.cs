using Common.Messaging;
using Missions;
using Missions.Battles;
using Missions.Messages;
using Moq;
using Newtonsoft.Json.Linq;
using ProtoBuf;
using System;
using System.IO;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class BattleShipDamageRouterTests
{
    private const string Own = "local";
    private const string Peer = "peer";
    private const int HostEpoch = 3;

    [Theory]
    [InlineData(true, false, BattleShipHitOrigin.LocalHit, BattleShipDamageRoute.Vanilla)]
    [InlineData(true, false, BattleShipHitOrigin.SelfTick, BattleShipDamageRoute.Vanilla)]
    [InlineData(true, false, BattleShipHitOrigin.ReplayedHit, BattleShipDamageRoute.Drop)]
    [InlineData(false, false, BattleShipHitOrigin.LocalHit, BattleShipDamageRoute.Send)]
    [InlineData(false, false, BattleShipHitOrigin.SelfTick, BattleShipDamageRoute.Drop)]
    [InlineData(false, false, BattleShipHitOrigin.ReplayedHit, BattleShipDamageRoute.Drop)]
    [InlineData(true, true, BattleShipHitOrigin.ReplayedHit, BattleShipDamageRoute.Vanilla)]
    [InlineData(false, true, BattleShipHitOrigin.SelfTick, BattleShipDamageRoute.Vanilla)]
    public void Route_RunsVanillaOnOwnHullsAndRoutedAppliesAndSendsOnlyLocalHitsOnForeignHulls(
        bool isOwnHull, bool isRoutedApply, BattleShipHitOrigin origin, BattleShipDamageRoute expected)
    {
        Assert.Equal(expected, BattleShipDamageRouter.Route(isOwnHull, isRoutedApply, origin));
    }

    [Theory]
    [InlineData(false, false, BattleShipHitOrigin.LocalHit)]
    [InlineData(true, false, BattleShipHitOrigin.LocalHit)]
    [InlineData(true, true, BattleShipHitOrigin.ReplayedHit)]
    public void AttackOrigin_TreatsOnlyAPuppetsAttackAsAReplay(bool hasAttacker, bool attackerIsPuppet, BattleShipHitOrigin expected)
    {
        Assert.Equal(expected, BattleShipDamageRouter.AttackOrigin(hasAttacker, attackerIsPuppet));
    }

    [Theory]
    [InlineData(true, false, BattleShipHitOrigin.LocalHit)]
    [InlineData(false, true, BattleShipHitOrigin.LocalHit)]
    [InlineData(false, false, BattleShipHitOrigin.ReplayedHit)]
    public void CollisionOrigin_BelongsToTheRammersOwner(bool isQueuedContact, bool hitterIsOwn, BattleShipHitOrigin expected)
    {
        Assert.Equal(expected, BattleShipDamageRouter.CollisionOrigin(isQueuedContact, hitterIsOwn));
    }

    [Fact]
    public void KeepsContactCollision_OnlyForAnOwnRammer()
    {
        Assert.True(BattleShipDamageRouter.KeepsContactCollision(rammerIsOwn: true));
        Assert.False(BattleShipDamageRouter.KeepsContactCollision(rammerIsOwn: false));
    }

    [Fact]
    public void ForeignHullHit_IsSentToEveryPeerWithTheHullsId()
    {
        var harness = new Harness();
        var hull = harness.Register(Peer, isNpcParty: false);

        harness.Broker.Publish(this, Hit(hull.Hull, BattleShipDamageKind.Fire, 30f));

        harness.Network.Verify(n => n.SendAll(It.Is<IMessage>(m => m is NetworkApplyShipDamage
            && ((NetworkApplyShipDamage)m).ShipId == hull.ShipId
            && ((NetworkApplyShipDamage)m).Kind == BattleShipDamageKind.Fire
            && ((NetworkApplyShipDamage)m).Damage == 30f
            && ((NetworkApplyShipDamage)m).SenderControllerId == Own
            && ((NetworkApplyShipDamage)m).HostEpoch == HostEpoch)), Times.Once);
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
        harness.Network.Verify(n => n.SendAll(It.IsAny<IMessage>()), Times.Never);
    }

    [Fact]
    public void HitOnAnUnregisteredHull_IsDropped()
    {
        var harness = new Harness();

        harness.Broker.Publish(this, Hit(ShipTestHulls.Create(), BattleShipDamageKind.Hull, 50f));

        harness.Network.Verify(n => n.SendAll(It.IsAny<IMessage>()), Times.Never);
        Assert.Equal("unregistered_hull", (string)harness.Inspect()["lastDrop"]!);
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
        Assert.Equal(1, (long)harness.Inspect()["dropped"]!);
        Assert.Equal("not_authority", (string)harness.Inspect()["lastDrop"]!);
    }

    [Fact]
    public void RoutedHit_OnAnUnknownHull_IsDropped()
    {
        var harness = new Harness();

        harness.Router.ApplyNetworkDamage(Damage(Guid.NewGuid(), BattleShipDamageKind.Hull, 10f));

        Assert.Equal("unknown_ship", (string)harness.Inspect()["lastDrop"]!);
    }

    [Theory]
    [InlineData(Own, true, HostEpoch, HostEpoch, 10f, null)]
    [InlineData(Own, true, 2, HostEpoch, 10f, "stale_epoch")]
    [InlineData(Own, true, 4, HostEpoch, 10f, "stale_epoch")]
    [InlineData(Own, true, 0, HostEpoch, 10f, null)]
    [InlineData(Own, true, 2, 0, 10f, null)]
    [InlineData(Own, false, 2, HostEpoch, 10f, null)]
    [InlineData(Peer, true, HostEpoch, HostEpoch, 10f, "not_authority")]
    [InlineData(Own, false, HostEpoch, HostEpoch, float.NaN, "invalid_damage")]
    [InlineData(Own, false, HostEpoch, HostEpoch, -1f, "invalid_damage")]
    public void ValidateDamage_AppliesOnlyAtTheAuthorityOfTheSendersHostEpoch(
        string authority, bool isNpcParty, int damageEpoch, int localEpoch, float amount, string expected)
    {
        var ship = new NetworkShipInfo(Guid.NewGuid(), authority, "MapEventParty_9", isNpcParty, ShipTestHulls.Create(), null);
        var damage = Damage(ship.ShipId, BattleShipDamageKind.Hull, amount, hostEpoch: damageEpoch);

        Assert.Equal(expected, BattleShipDamageRouter.ValidateDamage(ship, Own, damage, localEpoch));
    }

    [Fact]
    public void Dispose_StopsRoutingHits()
    {
        var harness = new Harness();
        var hull = harness.Register(Peer, isNpcParty: false);

        harness.Router.Dispose();
        harness.Broker.Publish(this, Hit(hull.Hull, BattleShipDamageKind.Hull, 5f));

        harness.Network.Verify(n => n.SendAll(It.IsAny<IMessage>()), Times.Never);
    }

    [Fact]
    public void NetworkApplyShipDamage_RoundTripsEveryField()
    {
        var damage = new NetworkApplyShipDamage(Guid.NewGuid(), Peer, BattleShipDamageKind.Collision, 120.5f, 7.25f,
            new Vec3(1f, -2f, 3.5f), Guid.NewGuid(), Guid.NewGuid(), isRamDamage: true, sailIndex: 2, hostEpoch: 4);

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
        Assert.Equal(4, copy.HostEpoch);
    }

    private static BattleShipHit Hit(MissionObject hull, BattleShipDamageKind kind, float damage) =>
        new BattleShipHit(hull, kind, damage, 0f, Vec3.Zero, null, null, false, -1);

    private static NetworkApplyShipDamage Damage(Guid shipId, BattleShipDamageKind kind, float damage, Guid hitterShipId = default,
        int hostEpoch = HostEpoch) =>
        new NetworkApplyShipDamage(shipId, Peer, kind, damage, 0f, Vec3.Zero, Guid.Empty, hitterShipId, false, -1, hostEpoch);

    private sealed class Harness
    {
        public Mock<IBattleNetwork> Network { get; } = new Mock<IBattleNetwork>();
        public Mock<INavalShipEngine> Engine { get; } = new Mock<INavalShipEngine>();
        public MessageBroker Broker { get; } = new MessageBroker();
        public NetworkShipRegistry Registry { get; } = new NetworkShipRegistry();
        public BattleShipDamageRouter Router { get; }

        public Harness()
        {
            var component = new Mock<ICoopMissionComponent>();
            component.SetupGet(c => c.ShipRegistry).Returns(Registry);
            component.SetupGet(c => c.AgentRegistry).Returns(Mock.Of<INetworkAgentRegistry>());

            var session = new Mock<IBattleSession>();
            session.SetupGet(s => s.OwnControllerId).Returns(Own);
            session.SetupGet(s => s.HostEpoch).Returns(HostEpoch);

            Router = new BattleShipDamageRouter(Network.Object, Broker, session.Object, component.Object, Engine.Object);
        }

        public NetworkShipInfo Register(string authority, bool isNpcParty)
        {
            var ship = new NetworkShipInfo(Guid.NewGuid(), authority, "MapEventParty_9", isNpcParty, ShipTestHulls.Create(), null);
            Registry.TryRegister(ship);
            return ship;
        }

        public JObject Inspect() => JObject.FromObject(Router.Inspect());
    }
}
