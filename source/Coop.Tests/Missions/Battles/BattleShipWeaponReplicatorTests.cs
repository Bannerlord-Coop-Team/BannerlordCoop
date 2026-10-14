using Common.Messaging;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Surrogates;
using Missions;
using Missions.Battles;
using Missions.Messages;
using Moq;
using Newtonsoft.Json.Linq;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class BattleShipWeaponReplicatorTests
{
    private const string Own = "local";
    private const string Peer = "peer";
    private const string Instance = "MapEvent_1";
    private const string WeaponKey = "3:ballista_holder/0:ship_ballista";
    private const int HostEpoch = 3;

    // M12: only the hull's owner simulates its weapon, so a copy's local AI never aims, loads or fires it.
    [Fact]
    public void Tick_SimulatesOnlyTheWeaponsOfHullsThisClientOwns()
    {
        var harness = new Harness();
        var own = harness.Register(Own, isNpcParty: false);
        harness.Register(Peer, isNpcParty: false);
        var swapping = harness.Register(Own, isNpcParty: true);
        harness.Ships.Setup(s => s.IsReplacingHull(swapping.Ship.ShipId)).Returns(true);
        IReadOnlyCollection<MissionObject> simulated = null;
        harness.Engine.Setup(e => e.SetSimulatedWeapons(It.IsAny<IReadOnlyCollection<MissionObject>>()))
            .Callback<IReadOnlyCollection<MissionObject>>(weapons => simulated = weapons);

        harness.Replicator.Tick(0.01f);
        harness.Replicator.Tick(0.01f);

        Assert.Equal(new MissionObject[] { own.Weapon }, simulated);
        harness.Engine.Verify(e => e.SetSimulatedWeapons(It.IsAny<IReadOnlyCollection<MissionObject>>()), Times.Once);
    }

    [Fact]
    public void Tick_HandsAnAiHullsWeaponToItsNewHost()
    {
        var harness = new Harness();
        var ai = harness.Register(Peer, isNpcParty: true);
        IReadOnlyCollection<MissionObject> simulated = null;
        harness.Engine.Setup(e => e.SetSimulatedWeapons(It.IsAny<IReadOnlyCollection<MissionObject>>()))
            .Callback<IReadOnlyCollection<MissionObject>>(weapons => simulated = weapons);
        harness.Replicator.Tick(0.01f);
        bool simulatedBefore = simulated.Contains(ai.Weapon);

        ai.Ship.CurrentAuthority = Own;
        harness.Replicator.Tick(0.01f);

        Assert.False(simulatedBefore);
        Assert.Equal(new MissionObject[] { ai.Weapon }, simulated);
    }

    [Fact]
    public void Tick_SendsAnOwnWeaponsState_OnlyWhenItChanges()
    {
        var harness = new Harness();
        var own = harness.Register(Own, isNpcParty: false);
        harness.Register(Peer, isNpcParty: false);
        harness.Engine.SetupSequence(e => e.ReadWeaponState(own.Weapon))
            .Returns(new BattleShipWeaponState(0.2f, 0.1f, 0, 5))
            .Returns(new BattleShipWeaponState(0.2f, 0.1f, 0, 5))
            .Returns(new BattleShipWeaponState(0.2f, 0.1f, 7, 4));

        for (int tick = 0; tick < 3; tick++)
            harness.Replicator.Tick(BattleShipWeaponReplicator.SendIntervalSeconds);

        var sent = harness.Sent<NetworkShipWeaponState>();
        Assert.Equal(new long[] { 1, 2 }, sent.Select(state => state.Revision));
        Assert.All(sent, state => Assert.Equal(own.Ship.ShipId, state.ShipId));
        Assert.All(sent, state => Assert.Equal(WeaponKey, state.WeaponKey));
        Assert.Equal(7, sent[1].State.WeaponState);
        harness.Engine.Verify(e => e.ReadWeaponState(It.Is<MissionObject>(weapon => weapon != own.Weapon)), Times.Never);
    }

    [Fact]
    public void LocalFire_OfAnOwnWeapon_SendsTheShotByShipAndWeaponKey()
    {
        var harness = new Harness();
        var own = harness.Register(Own, isNpcParty: false);
        var gunner = CreateAgent();
        var gunnerId = Guid.NewGuid();
        var gunnerInfo = new CoopAgentInfo(Own, Own, Instance, gunner, gunnerId, 1);
        harness.Agents.Setup(a => a.TryGetAgentInfo(gunner, out gunnerInfo)).Returns(true);

        harness.Fire(own.Weapon, gunner);

        var shot = Assert.Single(harness.Sent<NetworkShipWeaponShot>());
        Assert.Equal(own.Ship.ShipId, shot.ShipId);
        Assert.Equal(WeaponKey, shot.WeaponKey);
        Assert.Equal(Own, shot.OwnerControllerId);
        Assert.Equal(HostEpoch, shot.HostEpoch);
        Assert.Equal(gunnerId, shot.ShooterAgentId);
        Assert.Equal("ballista_projectile", shot.MissileItemId);
    }

    [Fact]
    public void LocalFire_OfACopysWeapon_SendsNothing()
    {
        var harness = new Harness();
        var copy = harness.Register(Peer, isNpcParty: false);

        harness.Fire(copy.Weapon, null);

        Assert.Empty(harness.Sent<NetworkShipWeaponShot>());
    }

    [Theory]
    [InlineData(Peer, Peer, true, null)]
    [InlineData(Peer, "someone_else", true, "not_authority")]
    [InlineData(Own, Own, true, "own_hull")]
    [InlineData(Peer, Peer, false, "invalid_shot")]
    public void ValidateShot_AcceptsOnlyAValidShotFromTheHullAuthority(string authority, string sender, bool valid, string expected)
    {
        var ship = new NetworkShipInfo(Guid.NewGuid(), authority, null, false, ShipTestHulls.Create(), null);

        Assert.Equal(expected, BattleShipWeaponReplicator.ValidateShot(ship, Own, Shot(ship.ShipId, sender, missileItemId: valid ? "ballista_projectile" : null)));
    }

    // M12 no double damage: the replayed bolt is the shooter's puppet's, never an agent this client drives, so the agent blow
    // and hull damage gates drop its hits; the real hit reaches the hull's owner from the shooter's owner.
    [Fact]
    public void AcceptShot_ReplaysWithTheShootersPuppet_NeverAnAgentThisClientDrives()
    {
        var harness = new Harness();
        var copy = harness.Register(Peer, isNpcParty: false);
        var puppet = CreateAgent();
        var puppetId = Guid.NewGuid();
        var puppetInfo = new CoopAgentInfo(Peer, Peer, Instance, puppet, puppetId, 1);
        harness.Agents.Setup(a => a.TryGetAgentInfo(puppetId, out puppetInfo)).Returns(true);
        var driven = CreateAgent();
        var drivenId = Guid.NewGuid();
        var drivenInfo = new CoopAgentInfo(Own, Own, Instance, driven, drivenId, 1);
        harness.Agents.Setup(a => a.TryGetAgentInfo(drivenId, out drivenInfo)).Returns(true);
        harness.Agents.Setup(a => a.IsLocallyControlled(drivenId)).Returns(true);
        var shooters = new List<Agent>();
        harness.Engine.Setup(e => e.ReplayWeaponShot(copy.Weapon, It.IsAny<NetworkShipWeaponShot>(), It.IsAny<Agent>(), It.IsAny<bool>()))
            .Callback<MissionObject, NetworkShipWeaponShot, Agent, bool>((_, _, shooter, _) => shooters.Add(shooter))
            .Returns(true);

        harness.Replicator.AcceptShot(Shot(copy.Ship.ShipId, Peer, shooterAgentId: puppetId));
        harness.Replicator.AcceptShot(Shot(copy.Ship.ShipId, Peer, shooterAgentId: drivenId));
        harness.Replicator.AcceptShot(Shot(copy.Ship.ShipId, Peer, shooterAgentId: Guid.NewGuid()));

        Assert.Equal(new[] { puppet, null, null }, shooters);
        Assert.Equal(BattleShipDamageRoute.Drop, BattleShipDamageRouter.Route(isOwnHull: true, isRoutedApply: false,
            BattleShipDamageRouter.AttackOrigin(hasAttacker: true, attackerIsPuppet: true)));
        harness.Network.Verify(n => n.SendAll(It.IsAny<IMessage>()), Times.Never);
    }

    [Fact]
    public void AcceptShot_DropsAShotForAnUnknownHullOrWeapon_OrFromASupersededHost()
    {
        var harness = new Harness();
        var copy = harness.Register(Peer, isNpcParty: false);
        var ai = harness.Register(Peer, isNpcParty: true);

        harness.Replicator.AcceptShot(Shot(Guid.NewGuid(), Peer));
        harness.Replicator.AcceptShot(Shot(copy.Ship.ShipId, Peer, weaponKey: "1:other_weapon"));
        harness.Replicator.AcceptShot(Shot(ai.Ship.ShipId, Peer, hostEpoch: HostEpoch - 1));

        harness.Engine.Verify(e => e.ReplayWeaponShot(It.IsAny<MissionObject>(), It.IsAny<NetworkShipWeaponShot>(), It.IsAny<Agent>(),
            It.IsAny<bool>()), Times.Never);
        Assert.Equal(3, (long)harness.Inspect()["shotsDropped"]!);
        Assert.Equal("shot_stale_epoch", (string)harness.Inspect()["lastDrop"]!);
    }

    [Fact]
    public void AcceptShot_PlaysTheLaunchEffectsOncePerVolley()
    {
        var harness = new Harness();
        var copy = harness.Register(Peer, isNpcParty: false);
        var effects = new List<bool>();
        harness.Engine.Setup(e => e.ReplayWeaponShot(copy.Weapon, It.IsAny<NetworkShipWeaponShot>(), It.IsAny<Agent>(), It.IsAny<bool>()))
            .Callback<MissionObject, NetworkShipWeaponShot, Agent, bool>((_, _, _, launch) => effects.Add(launch))
            .Returns(true);

        harness.Replicator.AcceptShot(Shot(copy.Ship.ShipId, Peer));
        harness.Replicator.AcceptShot(Shot(copy.Ship.ShipId, Peer));
        harness.Replicator.Tick(BattleShipWeaponReplicator.VolleySeconds + 0.1f);
        harness.Replicator.AcceptShot(Shot(copy.Ship.ShipId, Peer));

        Assert.Equal(new[] { true, false, true }, effects);
    }

    [Theory]
    [InlineData(Peer, Peer, false, 5, 4, 0, 0, null)]
    [InlineData(Peer, "someone_else", false, 5, 4, 0, 0, "not_authority")]
    [InlineData(Own, Own, false, 5, 4, 0, 0, "own_hull")]
    [InlineData(Peer, Peer, false, 4, 4, 0, 0, "stale")]
    [InlineData(Peer, Peer, true, 1, 9, 4, 3, null)]
    [InlineData(Peer, Peer, true, 9, 1, 2, 3, "stale_epoch")]
    public void ValidateState_AcceptsOnlyTheAuthoritysNewerState(string authority, string sender, bool isNpcParty, long revision,
        long appliedRevision, int epoch, int appliedEpoch, string expected)
    {
        var ship = new NetworkShipInfo(Guid.NewGuid(), authority, null, isNpcParty, ShipTestHulls.Create(), null);
        var state = new NetworkShipWeaponState(ship.ShipId, WeaponKey, sender, revision, epoch, new BattleShipWeaponState(0f, 0f, 0, 3));

        Assert.Equal(expected, BattleShipWeaponReplicator.ValidateState(ship, Own, state, appliedRevision, appliedEpoch));
    }

    [Fact]
    public void ValidateState_RejectsAnInvalidState()
    {
        var ship = new NetworkShipInfo(Guid.NewGuid(), Peer, null, false, ShipTestHulls.Create(), null);
        var state = new NetworkShipWeaponState(ship.ShipId, WeaponKey, Peer, 1, 0, new BattleShipWeaponState(float.NaN, 0f, 0, 3));

        Assert.Equal("invalid_state", BattleShipWeaponReplicator.ValidateState(ship, Own, state, 0, 0));
    }

    [Fact]
    public void AcceptState_ForAHullNotSpawnedYet_AppliesOnceItRegisters_AndHandsOverThePreviousState()
    {
        var harness = new Harness();
        var shipId = Guid.NewGuid();
        var loaded = new BattleShipWeaponState(0.3f, 0.1f, 0, 5);
        var reloading = new BattleShipWeaponState(0.3f, 0.1f, 7, 4);
        harness.Replicator.AcceptState(new NetworkShipWeaponState(shipId, WeaponKey, Peer, 1, 0, loaded));
        harness.Engine.Verify(e => e.ApplyWeaponState(It.IsAny<MissionObject>(), It.IsAny<BattleShipWeaponState>(),
            It.IsAny<BattleShipWeaponState>()), Times.Never);

        var copy = harness.Register(Peer, isNpcParty: false, shipId);
        harness.Replicator.Tick(0.01f);
        harness.Replicator.AcceptState(new NetworkShipWeaponState(shipId, WeaponKey, Peer, 2, 0, reloading));

        harness.Engine.Verify(e => e.ApplyWeaponState(copy.Weapon, loaded, null), Times.Once);
        harness.Engine.Verify(e => e.ApplyWeaponState(copy.Weapon, reloading, loaded), Times.Once);
    }

    [Fact]
    public void ShotAndState_KeepTheirFieldsOverTheWire()
    {
        _ = new SurrogateCollection();
        var shipId = Guid.NewGuid();
        var shooterId = Guid.NewGuid();

        var shot = Serializer.DeepClone(Shot(shipId, Peer, shooterAgentId: shooterId));
        var state = Serializer.DeepClone(new NetworkShipWeaponState(shipId, WeaponKey, Peer, 7, 2, new BattleShipWeaponState(0.5f, 0.2f, 7, 3)));

        Assert.Equal(shipId, shot.ShipId);
        Assert.Equal(WeaponKey, shot.WeaponKey);
        Assert.Equal(shooterId, shot.ShooterAgentId);
        Assert.Equal(new Vec3(1f, 2f, 3f), shot.Position);
        Assert.Equal(new Vec3(0f, 1f, 0f), shot.Direction);
        Assert.Equal(62f, shot.Speed);
        Assert.Equal("ballista_projectile", shot.MissileItemId);
        Assert.Equal(7, state.Revision);
        Assert.Equal(2, state.HostEpoch);
        Assert.Equal(0.5f, state.State.Direction);
        Assert.Equal(7, state.State.WeaponState);
        Assert.Equal(3, state.State.AmmoCount);
    }

    [Fact]
    public void Dispose_HandsWeaponSimulationBack()
    {
        var harness = new Harness();

        harness.Replicator.Dispose();

        harness.Engine.Verify(e => e.ClearSimulatedWeapons(), Times.Once);
    }

    private static NetworkShipWeaponShot Shot(Guid shipId, string sender, Guid shooterAgentId = default, string weaponKey = WeaponKey,
        int hostEpoch = HostEpoch, string missileItemId = "ballista_projectile") =>
        new NetworkShipWeaponShot(shipId, weaponKey, sender, hostEpoch, shooterAgentId, new Vec3(1f, 2f, 3f), new Vec3(0f, 1f, 0f),
            Mat3.Identity, 60f, 62f, missileItemId);

    private static Agent CreateAgent() => (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));

    private sealed class Harness
    {
        public Mock<IBattleNetwork> Network { get; } = new Mock<IBattleNetwork>();
        public Mock<INavalShipEngine> Engine { get; } = new Mock<INavalShipEngine>();
        public Mock<INetworkAgentRegistry> Agents { get; } = new Mock<INetworkAgentRegistry>();
        public Mock<IBattleShipReplicator> Ships { get; } = new Mock<IBattleShipReplicator>();
        public MessageBroker Broker { get; } = new MessageBroker();
        public NetworkShipRegistry Registry { get; } = new NetworkShipRegistry();
        public BattleShipWeaponReplicator Replicator { get; }

        public Harness()
        {
            var component = new Mock<ICoopMissionComponent>();
            component.SetupGet(c => c.ShipRegistry).Returns(Registry);
            component.SetupGet(c => c.AgentRegistry).Returns(Agents.Object);

            var session = new Mock<IBattleSession>();
            session.SetupGet(s => s.OwnControllerId).Returns(Own);
            session.SetupGet(s => s.HostEpoch).Returns(HostEpoch);
            session.SetupGet(s => s.InstanceId).Returns(Instance);
            session.Setup(s => s.IsOwn(It.IsAny<string>())).Returns((string id) => id == Own);

            Replicator = new BattleShipWeaponReplicator(Network.Object, Broker, session.Object, component.Object, Engine.Object,
                Ships.Object, new HostEpochPolicy());
        }

        /// <summary>A registered hull carrying a ship siege weapon at <see cref="WeaponKey"/>.</summary>
        public (NetworkShipInfo Ship, MissionObject Weapon) Register(string authority, bool isNpcParty, Guid shipId = default)
        {
            var ship = new NetworkShipInfo(shipId == default ? Guid.NewGuid() : shipId, authority, "MapEventParty_2", isNpcParty,
                ShipTestHulls.Create(), null);
            Registry.TryRegister(ship);
            MissionObject weapon = ShipTestHulls.CreateWeapon();
            string key = WeaponKey;
            Engine.Setup(e => e.TryGetWeapon(ship.Hull, out weapon, out key)).Returns(true);
            return (ship, weapon);
        }

        /// <summary>The projectile capture's event for a launch of <paramref name="weapon"/>.</summary>
        public void Fire(MissionObject weapon, Agent shooter)
        {
            var missile = (ItemObject)FormatterServices.GetUninitializedObject(typeof(ItemObject));
            missile.StringId = "ballista_projectile";
            Broker.Publish(this, new SiegeWeaponFired((RangedSiegeWeapon)weapon, shooter, new Vec3(1f, 2f, 3f), new Vec3(0f, 1f, 0f),
                Mat3.Identity, 60f, 62f, missile));
        }

        public List<T> Sent<T>() => Network.Invocations
            .Where(invocation => invocation.Method.Name == nameof(IBattleNetwork.SendAll))
            .Select(invocation => invocation.Arguments[0])
            .OfType<T>()
            .ToList();

        public JObject Inspect() => JObject.FromObject(Replicator.Inspect());
    }
}
