using Common.Messaging;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Environment.Mock;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.MapEvents;
using Missions;
using Missions.Battles;
using Missions.Messages;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.Players;
using Moq;
using Newtonsoft.Json.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions.Naval;

/// <summary>
/// Naval battle E2E base. Every client opens a headless mission on the mission mesh with its own
/// <see cref="CoopBattleController"/> and the DLC-free ship services over a <see cref="FakeNavalShipEngine"/>, so hull,
/// station, damage and host traffic travels as real serialized messages through the real handlers. Time only moves
/// through <see cref="NavalPeer.Tick"/>, one mission clock per client.
/// </summary>
public abstract class NavalBattleTestEnvironment : MissionTestEnvironment
{
    /// <summary>The AI party the battle host fields hulls for; registered like a player so its map event party resolves.</summary>
    protected const string PirateId = "pirates";

    protected const float TickSeconds = 0.05f;

    protected NavalBattleTestEnvironment(ITestOutputHelper output, int numClients) : base(output, numClients) { }

    /// <summary>
    /// Stands up a naval battle with one client per player, in order; the first player is the battle host and the rest are
    /// its successors. Players in <paramref name="lateJoiners"/> stay off the mesh until <see cref="NavalBattle.Join"/>.
    /// </summary>
    protected NavalBattle StartNavalBattle((string Id, BattleSideEnum Side)[] players, int hostEpoch = 1, params string[] lateJoiners)
    {
        var battleIds = players.Select(player => player.Id).ToList();
        battleIds.Insert(1, PirateId);
        var (mapEventId, partyIds) = SetupCoopBattle(battleIds.ToArray());

        var clients = Clients.ToArray();
        for (int i = 0; i < players.Length; i++)
            SetControllerId(clients[i], players[i].Id);

        var battle = new NavalBattle(this, mapEventId, players.Select(player => player.Id).ToArray(), hostEpoch);
        clients[0].Call(() =>
        {
            battle.PirateMapEventPartyId = MapEventPartyIdOf(clients[0], partyIds[1]);
            for (int i = 0; i < players.Length; i++)
                battle.PartyIds[players[i].Id] = MapEventPartyIdOf(clients[0], partyIds[i == 0 ? 0 : i + 1]);
        });

        BattleSpawnGate.BeginBattle(mapEventId);
        for (int i = 0; i < players.Length; i++)
        {
            battle.Players[players[i].Id] = (clients[i], players[i].Side);
            if (!lateJoiners.Contains(players[i].Id)) battle.Join(players[i].Id);
        }

        return battle;
    }

    /// <summary>A two-player battle at sea or on land whose host the server elects from the mission-ready order, every member on the mesh.</summary>
    protected NavalBattle StartServerBattle(bool atSea, out NavalPeer a, out NavalPeer b, out Func<int> concluded)
    {
        var battle = StartNavalBattle(new[] { ("A", BattleSideEnum.Attacker), ("B", BattleSideEnum.Attacker) });
        a = battle["A"];
        b = battle["B"];
        Server.Call(() => Server.GetRegisteredObject<MapEvent>(battle.MapEventId).Position =
            new CampaignVec2(Vec2.Zero, isOnLand: !atSea));

        foreach (var peer in battle.Peers)
        {
            var member = peer;
            Server.Call(() => Server.Resolve<IPlayerManager>().SetPeer(member.Id, member.Instance.NetPeer));
            EnterBattle(member.Instance, battle.MapEventId);
            Server.SimulateMessage(member.Instance.NetPeer, new NetworkMissionEntered(member.Id, battle.MapEventId));
        }
        AssertHost(Server, battle.MapEventId, "A", "B");

        int conclusions = 0;
        Server.Resolve<IMessageBroker>().Subscribe<MapEventConcluded>(_ => conclusions++);
        concluded = () => conclusions;
        return battle;
    }

    /// <summary>The battle host's end logic resolving the battle: its committer reports the result and snapshots it to the peers.</summary>
    protected void ResolveOnHost(NavalPeer host, BattleState state) => host.Call(() =>
    {
        host.Controller.ResultCommitter.AcceptResolvedState(state);
        host.Controller.ResultCommitter.ReportAcceptedResult();
    }, VictoryConclusionDisabledMethods());

    protected void AssertMapEventRemoved(string mapEventId) =>
        Server.Call(() => Assert.False(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out _)));

    /// <summary>
    /// A client's naval end tally for a side, counted over its hulls the way <c>CoopNavalBattleEndLogic</c> counts them (a
    /// copy's reserve as its owner reports it); <paramref name="crew"/> holds the live crew per hull and defaults to none.
    /// </summary>
    protected static NavalSideTally EndTally(NavalPeer peer, BattleSideEnum side, int liveHumans,
        IReadOnlyDictionary<Guid, int>? crew = null)
    {
        NavalSideTally tally = null!;
        peer.Call(() =>
        {
            var hulls = peer.Registry.Ships.Select(ship =>
            {
                var hull = peer.Engine.Of(ship.Hull);
                int crewAboard = crew != null && crew.TryGetValue(ship.ShipId, out int count) ? count : 0;
                int reserve = hull.IsCopy ? peer.Ships.ReportedReserveOf(ship.Hull) : peer.Engine.ReadSpawnableReserve(ship.Hull);
                return new NavalHullCount(hull.Side, hull.SinkingState == BattleShipCondition.Sunk, crewAboard, reserve);
            }).ToList();
            tally = NavalSideTally.Count(side, liveHumans, hulls, peer.MigrationReserve.PendingReserve(side));
        });
        return tally;
    }

    private static string MapEventPartyIdOf(EnvironmentInstance instance, string partyId)
    {
        Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
        var mapEventParty = party.Party.MapEventSide.Parties.Single(candidate => candidate.Party == party.Party);
        Assert.True(instance.ObjectManager.TryGetId(mapEventParty, out var mapEventPartyId));
        return mapEventPartyId;
    }

    /// <summary>A running naval battle: its clients, the current host assignment and the mission engine shims.</summary>
    protected sealed class NavalBattle : IDisposable
    {
        private readonly NavalBattleTestEnvironment environment;
        private readonly MissionEngineFixture fixture = new();
        private readonly Dictionary<string, NavalPeer> peers = new();

        public NavalBattle(NavalBattleTestEnvironment environment, string mapEventId, string[] successionOrder, int hostEpoch)
        {
            this.environment = environment;
            MapEventId = mapEventId;
            HostId = successionOrder[0];
            Successors = successionOrder.Skip(1).ToArray();
            HostEpoch = hostEpoch;
        }

        public string MapEventId { get; }
        public string HostId { get; private set; }
        public string[] Successors { get; private set; }
        public int HostEpoch { get; private set; }
        public string PirateMapEventPartyId { get; set; } = null!;
        public Dictionary<string, string> PartyIds { get; } = new();
        public Dictionary<string, (EnvironmentInstance Instance, BattleSideEnum Side)> Players { get; } = new();

        public NavalPeer this[string id] => peers[id];

        /// <summary>The clients on the mesh.</summary>
        public IEnumerable<NavalPeer> Peers => peers.Values.Where(peer => peer.Network.IsStarted);

        /// <summary>Opens the player's mission on the mesh with the battle's current host assignment.</summary>
        public NavalPeer Join(string id)
        {
            var (instance, side) = Players[id];
            NavalPeer peer = null!;
            instance.Call(() =>
            {
                var mission = CreateConnectedMission(fixture, instance, MapEventId);
                mission.PlayerTeam = side == BattleSideEnum.Attacker ? mission.AttackerTeam : mission.DefenderTeam;
                mission.AddTeam(BattleSideEnum.Attacker);
                mission.AddTeam(BattleSideEnum.Attacker);
                mission.AddTeam(BattleSideEnum.Defender);
                mission.AddTeam(BattleSideEnum.Defender);

                var controller = instance.Resolve<CoopBattleController>();
                controller.Session.TryBegin(MapEventId);
                instance.Resolve<IBattleHostRegistry>().Set(MapEventId, new BattleHostAssignment(HostId, Successors, HostEpoch));
                peer = new NavalPeer(id, side, instance, mission, controller, new FakeNavalShipEngine());
            });

            peers[id] = peer;
            return peer;
        }

        /// <summary>Tells every other client on the mesh that <paramref name="entered"/> joined, as the mission lifecycle does.</summary>
        public void AnnounceEntry(NavalPeer entered)
        {
            foreach (var peer in Peers.Where(peer => peer != entered))
                peer.Instance.Call(() => peer.Broker.Publish(peer, new NetworkMissionPeerEntered(entered.Id, MapEventId)));
        }

        /// <summary>Drops <paramref name="departed"/> off the mesh and hands the battle to <paramref name="newHost"/> at the next epoch.</summary>
        public void MigrateHost(NavalPeer departed, string newHost)
        {
            departed.Instance.Call(departed.Network.Stop);
            foreach (var peer in Peers)
                peer.Instance.Call(() => peer.Broker.Publish(peer, new MissionPeerDisconnected(departed.Id, MapEventId)));

            HostId = newHost;
            Successors = Successors.Where(id => id != newHost && id != departed.Id).ToArray();
            HostEpoch++;
            foreach (var peer in Peers)
                peer.Instance.Call(() => peer.Broker.Publish(peer,
                    new NetworkBattleHostAssigned(MapEventId, HostId, Successors, HostEpoch)));
        }

        /// <summary>Advances every client on the mesh by <paramref name="ticks"/> mission ticks.</summary>
        public void TickAll(int ticks = 1, float dt = TickSeconds)
        {
            for (int tick = 0; tick < ticks; tick++)
                foreach (var peer in Peers)
                    peer.Tick(dt);
        }

        /// <summary>Finishes every client's deployment, which announces its hulls at the next tick.</summary>
        public void CommitAll()
        {
            foreach (var peer in Peers)
                peer.Commit();
        }

        public Guid FieldPlayerHull(NavalPeer owner, Vec3 position) =>
            owner.FieldOwnHull(owner.Side, position, PartyIds[owner.Id]);

        public Guid FieldAiHull(NavalPeer host, BattleSideEnum side, Vec3 position, int? formationIndex = null) =>
            host.FieldAiHull(side, position, PirateMapEventPartyId, formationIndex);

        public void Dispose()
        {
            foreach (var peer in peers.Values)
                peer.Instance.Call(peer.DisposeShipServices);
            BattleSpawnGate.EndBattle();
            fixture.Dispose();
        }
    }

    /// <summary>One client in a naval battle: its mission, controller, fake ship engine and the ship services under test.</summary>
    protected sealed class NavalPeer
    {
        public NavalPeer(string id, BattleSideEnum side, EnvironmentInstance instance, MockMission mission,
            CoopBattleController controller, FakeNavalShipEngine engine)
        {
            Id = id;
            Side = side;
            Instance = instance;
            Mission = mission;
            Controller = controller;
            Engine = engine;
            Network = instance.Resolve<MockBattleNetwork>();
            Broker = instance.Resolve<IMessageBroker>();
            Ships = new BattleShipReplicator(Network, Broker, controller.Session, controller.Deployment, controller.MissionComponent,
                engine, instance.Resolve<IBattleTeamResolver>(), instance.ObjectManager, controller.HostEpochPolicy,
                controller.ResultCommitter);
            Stations = new AgentStationUseReplicator(Network, Broker, controller.Session, controller.MissionComponent, engine,
                controller.Deployment);
            Damage = new BattleShipDamageRouter(Network, Broker, controller.Session, controller.MissionComponent, engine);
            var reinforcements = new Mock<IReinforcementFielder>();
            reinforcements.Setup(fielder => fielder.TakeMigrationReserve(It.IsAny<BattleSideEnum>())).Returns(() =>
            {
                var recovered = RecoveredReserve.ToList();
                RecoveredReserve.Clear();
                return recovered;
            });
            MigrationReserve = new NavalMigrationReserveFielder(controller.Session, reinforcements.Object,
                controller.MissionComponent.ShipRegistry, engine, Ships);
        }

        public string Id { get; }
        public BattleSideEnum Side { get; }
        public EnvironmentInstance Instance { get; }
        public MockMission Mission { get; }
        public CoopBattleController Controller { get; }
        public FakeNavalShipEngine Engine { get; }
        public MockBattleNetwork Network { get; }
        public IMessageBroker Broker { get; }
        public BattleShipReplicator Ships { get; }
        public AgentStationUseReplicator Stations { get; }
        public BattleShipDamageRouter Damage { get; }
        public NavalMigrationReserveFielder MigrationReserve { get; }
        /// <summary>The AI reserve the reinforcement fielder's roster recovery hands over at the next tick, as after a promotion.</summary>
        public List<CoopAgentOrigin> RecoveredReserve { get; } = new();
        public INetworkShipRegistry Registry => Controller.MissionComponent.ShipRegistry;
        public INetworkAgentRegistry Agents => Controller.MissionComponent.AgentRegistry;

        public void Call(Action action, IEnumerable<MethodBase>? disabledMethods = null) => Instance.Call(action, disabledMethods);

        /// <summary>One naval services tick in the live order: the ship replicator, station use, then the migration reserve.</summary>
        public void Tick(float dt = TickSeconds) => Call(() =>
        {
            Ships.Tick(dt);
            Stations.Tick(dt);
            MigrationReserve.Tick();
        });

        public void Ticks(int count)
        {
            for (int tick = 0; tick < count; tick++)
                Tick();
        }

        /// <summary>
        /// The battle controller's tick, which drains buffered puppets. Kept short of its four-second team diagnostic,
        /// which reads native team state the headless mission does not have.
        /// </summary>
        public void TickController() => Call(() => Controller.OnMissionTick(TickSeconds));

        public void Commit() => Call(() => Controller.Deployment.OnLocalDeploymentFinished());

        /// <summary>A hull vanilla's team setup fielded for this client, registered as the spawn postfix does.</summary>
        public Guid FieldOwnHull(BattleSideEnum side, Vec3 position, string mapEventPartyId)
        {
            var shipId = Guid.NewGuid();
            Call(() =>
            {
                var hull = Engine.Field(side, position);
                Assert.True(Registry.TryRegister(new NetworkShipInfo(shipId, Id, mapEventPartyId, false, hull.Hull, null)));
            });
            return shipId;
        }

        /// <summary>
        /// [Battle host] An AI party's hull, registered and announced through the replicator; with a
        /// <paramref name="formationIndex"/> it takes that formation of the side's main team, as the NPC fleet spawner does.
        /// </summary>
        public Guid FieldAiHull(BattleSideEnum side, Vec3 position, string mapEventPartyId, int? formationIndex = null)
        {
            var shipId = Guid.Empty;
            Call(() =>
            {
                var team = formationIndex == null ? null
                    : side == BattleSideEnum.Attacker ? Mission.AttackerTeam.Shell : Mission.DefenderTeam.Shell;
                var hull = Engine.Field(side, position, formationIndex ?? 0, team);
                shipId = Ships.RegisterNpcHull(hull.Hull, hull.Formation!, mapEventPartyId);
            });
            Assert.NotEqual(Guid.Empty, shipId);
            return shipId;
        }

        public NetworkShipInfo Ship(Guid shipId)
        {
            NetworkShipInfo ship = null!;
            Call(() => Assert.True(Registry.TryGet(shipId, out ship), $"{Id} has no hull {shipId}"));
            return ship;
        }

        public bool Has(Guid shipId)
        {
            bool has = false;
            Call(() => has = Registry.TryGet(shipId, out _));
            return has;
        }

        /// <summary>This client's local hull for a ship id.</summary>
        public FakeHull Hull(Guid shipId) => Engine.Of(Ship(shipId).Hull);

        /// <summary>The replicator's diagnostics entry for a ship.</summary>
        public JToken Stream(Guid shipId)
        {
            JToken entry = null!;
            Call(() => entry = JObject.FromObject(Ships.Inspect())["ships"]!.Single(ship => (Guid)ship["shipId"]! == shipId));
            return entry;
        }

        public JObject StationStats()
        {
            JObject stats = null!;
            Call(() => stats = JObject.FromObject(Stations.Inspect()));
            return stats;
        }

        /// <summary>
        /// A headless agent registered under <paramref name="authority"/>, as a replicated crew member or own troop; with a
        /// <paramref name="team"/> it joins that team, as crew join their hull's team.
        /// </summary>
        public Agent RegisterAgent(Guid agentId, string authority, MockTeam? team = null)
        {
            Agent agent = null!;
            Call(() =>
            {
                var build = new AgentBuildData(Game.Current.PlayerTroop)
                    .Controller(authority == Id ? AgentControllerType.AI : AgentControllerType.None);
                agent = Mission.SpawnAgent(team == null ? build : build.Team(team.Shell));
                Assert.True(Agents.TryRegisterAgent(authority, agentId, agent));
            });
            return agent;
        }

        /// <summary>The use/release patch's event for an agent this client drives at a station of its local hull.</summary>
        public void UseStation(Agent agent, UsableMissionObject point, bool inUse) =>
            Call(() => Broker.Publish(this, new AgentStationUseChanged(agent, point, inUse)));

        /// <summary>The ship damage patch's event for a hit this client decided on its local hull.</summary>
        public void Hit(Guid shipId, float damage) => Call(() =>
        {
            Assert.True(Registry.TryGet(shipId, out var ship));
            Broker.Publish(this, new BattleShipHit(ship.Hull, BattleShipDamageKind.Hull, damage, 0f, Vec3.Zero, null!, null!,
                false, -1));
        });

        /// <summary>The samples this client sent for a ship.</summary>
        public IReadOnlyList<NetworkBattleShipSample> SentSamples(Guid shipId) =>
            Network.NetworkSentMessages.GetMessages<NetworkBattleShipSample>().Where(sample => sample.ShipId == shipId).ToList();

        internal void DisposeShipServices()
        {
            Damage.Dispose();
            Stations.Dispose();
            Ships.Dispose();
        }
    }
}
