using Common.Messaging;
using E2E.Tests.Environment.MockEngine;
using E2E.Tests.Util;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.Players;
using HarmonyLib;
using Missions;
using Missions.Battles;
using Missions.Data;
using Missions.Messages;
using Moq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

public class BattleFormationSyncTests : MissionTestEnvironment
{
    public BattleFormationSyncTests(ITestOutputHelper output) : base(output) { }

    // Catch-up snapshots refresh existing remote agents but cannot overwrite local or newer-authority state.
    [Theory]
    [InlineData("owner", 0, "owner", 0, 7, 7)]
    [InlineData("owner", 0, "owner", 0, -1, -1)]
    [InlineData("peer", 0, "peer", 0, 7, 0)]
    [InlineData("owner", 1, "owner", 0, 7, 0)]
    [InlineData("owner", 0, "former", 0, 7, 0)]
    public void CatchUp_RefreshesOnlyCurrentRemoteMembership(
        string owner, long revision, string sender, long snapshotRevision, int slot, int expectedSlot)
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        peer.Call(() =>
        {
            var mission = fixture.CreateMission(peer);
            var broker = peer.Resolve<IMessageBroker>();
            var registry = peer.Resolve<INetworkAgentRegistry>();
            var session = Mock.Of<IBattleSession>(value => value.InstanceId == "battle" && value.OwnControllerId == "peer");
            Mock.Get(session).Setup(value => value.IsOwn(It.IsAny<string>())).Returns((string id) => id == "peer");
            using var spawner = new PuppetSpawner(broker, peer.ObjectManager, peer.Resolve<IPlayerManager>(),
                peer.Resolve<ICoopMissionComponent>(), session, new CasualtyAttributionMap(),
                Mock.Of<IBattleDeploymentCoordinator>(), new AgentFormationAssigner(), new BattleAgentBudget(),
                peer.Resolve<IMissionWeaponDataMapper>());
            var id = Guid.NewGuid();
            var agent = mission.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                .Controller(AgentControllerType.None).Team(mission.DefenderTeam.Shell));
            agent.Formation = mission.DefenderTeam.GetFormation(FormationClass.Infantry).Shell;
            Assert.True(registry.TryRegisterAgent(owner, "original", "original", id, 0, agent, revision));
            var record = new BattleAgentSpawnData(id, "troop", default, BattleSideEnum.Defender,
                100f, sender, null, 0, new Equipment(), default, null, formationIndex: slot,
                originalOwnerControllerId: "original", authorityRevision: snapshotRevision);

            broker.Publish(this, new NetworkSpawnBattleAgents(new[] { record }));

            if (expectedSlot == -1) Assert.Null(agent.Formation);
            else Assert.Same(mission.DefenderTeam.GetFormation((FormationClass)expectedSlot).Shell, agent.Formation);
            Assert.True(registry.TryGetAgentInfo(id, out var info));
            Assert.Same(agent, info.Agent);
            Assert.Equal(owner, info.CurrentAuthority);
            Assert.Equal(revision, info.AuthorityRevision);
        });
    }

    // Retrying an old buffered spawn must not undo a newer live formation update.
    [Fact]
    public void BufferedSpawn_DoesNotRefreshExistingMembership()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        peer.Call(() =>
        {
            var mission = fixture.CreateMission(peer);
            var broker = peer.Resolve<IMessageBroker>();
            var registry = peer.Resolve<INetworkAgentRegistry>();
            var session = Mock.Of<IBattleSession>(value => value.InstanceId == "battle" && value.OwnControllerId == "peer");
            using var spawner = new PuppetSpawner(broker, peer.ObjectManager, peer.Resolve<IPlayerManager>(),
                peer.Resolve<ICoopMissionComponent>(), session, new CasualtyAttributionMap(),
                Mock.Of<IBattleDeploymentCoordinator>(), new AgentFormationAssigner(),
                Mock.Of<IBattleAgentBudget>(value => value.SlotsForEquipment(It.IsAny<Equipment>()) == 1),
                peer.Resolve<IMissionWeaponDataMapper>());
            var id = Guid.NewGuid();
            var record = new BattleAgentSpawnData(id, "troop", default, BattleSideEnum.Defender,
                100f, "owner", null, 0, new Equipment(), default, null, formationIndex: 0);
            broker.Publish(this, new NetworkSpawnBattleAgents(new[] { record }));
            var pending = (List<BattleAgentSpawnData>)AccessTools.Field(typeof(PuppetSpawner), "pendingPuppets").GetValue(spawner);
            Assert.Single(pending);
            var agent = mission.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                .Controller(AgentControllerType.None).Team(mission.DefenderTeam.Shell));
            Assert.True(registry.TryRegisterAgent("owner", id, agent));
            broker.Publish(this, new NetworkBattleAgentFormations("battle", "owner",
                new[] { new BattleAgentFormationData(id, 7, 0) }));

            spawner.DrainPendingPuppets();

            Assert.Empty(pending);
            Assert.Same(mission.DefenderTeam.GetFormation((FormationClass)7).Shell, agent.Formation);
        });
    }

    [Fact]
    public void BulkRegroup_CoalescesLatestSlotAndBoundsBatches()
    {
        using var fixture = new MissionEngineFixture();
        var owner = Clients.First();
        owner.Call(() =>
        {
            var mission = fixture.CreateMission(owner);
            var broker = owner.Resolve<IMessageBroker>();
            var registry = owner.Resolve<INetworkAgentRegistry>();
            var network = new Mock<IBattleNetwork>();
            var session = Mock.Of<IBattleSession>(value => value.OwnControllerId == "owner" && value.InstanceId == "battle");
            using var replicator = new OwnedAgentReplicator(network.Object, broker, owner.ObjectManager,
                owner.Resolve<ICoopMissionComponent>(), session, new CasualtyAttributionMap(),
                Mock.Of<IBattleDeploymentCoordinator>(), new BattleAgentSpawnBatchCodec(), owner.Resolve<IMissionWeaponDataMapper>());
            var target = mission.DefenderTeam.GetFormation(FormationClass.Ranged).Shell;
            AccessTools.Field(typeof(Formation), nameof(Formation.FormationIndex)).SetValue(target, FormationClass.Ranged);

            for (int i = 0; i < NetworkBattleAgentFormations.MaxUpdates + 1; i++)
            {
                var agent = mission.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                    .Controller(AgentControllerType.AI).Team(mission.DefenderTeam.Shell));
                Assert.True(registry.TryRegisterAgent("owner", Guid.NewGuid(), agent));
                broker.Publish(this, new BattleAgentFormationChanged(agent));
                agent.Formation = target;
                broker.Publish(this, new BattleAgentFormationChanged(agent));
            }
            var puppet = mission.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                .Controller(AgentControllerType.None).Team(mission.DefenderTeam.Shell));
            Assert.True(registry.TryRegisterAgent("peer", Guid.NewGuid(), puppet));
            broker.Publish(this, new BattleAgentFormationChanged(puppet));
            Assert.Empty(network.Invocations);

            replicator.FlushPendingFormations();
            var batches = network.Invocations.Select(call => call.Arguments[0]).OfType<NetworkBattleAgentFormations>().ToArray();
            Assert.Equal(2, batches.Length);
            Assert.Equal(NetworkBattleAgentFormations.MaxUpdates, batches[0].Agents.Length);
            Assert.Single(batches[1].Agents);
            Assert.All(batches.SelectMany(batch => batch.Agents), data => Assert.Equal((int)FormationClass.Ranged, data.FormationIndex));
            replicator.FlushPendingFormations();
            Assert.Equal(2, network.Invocations.Count);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WireUpdate_BuffersUntilRegisteredAndClearsFormation(bool beforeRegistration)
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        peer.Call(() =>
        {
            var mission = fixture.CreateMission(peer);
            var broker = peer.Resolve<IMessageBroker>();
            var registry = peer.Resolve<INetworkAgentRegistry>();
            var session = Mock.Of<IBattleSession>(value => value.InstanceId == "battle" && value.OwnControllerId == "peer");
            Mock.Get(session).Setup(value => value.IsOwn(It.IsAny<string>())).Returns((string id) => id == "peer");
            using var spawner = new PuppetSpawner(broker, peer.ObjectManager, peer.Resolve<IPlayerManager>(),
                peer.Resolve<ICoopMissionComponent>(), session, new CasualtyAttributionMap(),
                Mock.Of<IBattleDeploymentCoordinator>(), new AgentFormationAssigner(), new BattleAgentBudget(),
                peer.Resolve<IMissionWeaponDataMapper>());
            var id = Guid.NewGuid();
            var agent = mission.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                .Controller(AgentControllerType.None).Team(mission.DefenderTeam.Shell));
            agent.Formation = mission.DefenderTeam.GetFormation(FormationClass.Infantry).Shell;
            if (!beforeRegistration) Assert.True(registry.TryRegisterAgent("owner", id, agent));

            var wire = new NetworkBattleAgentFormations("battle", "owner",
                new[] { new BattleAgentFormationData(id, (int)FormationClass.Ranged, 0) });
            broker.Publish(this, ProtoBuf.Serializer.DeepClone(wire));
            if (beforeRegistration)
            {
                Assert.Same(mission.DefenderTeam.GetFormation(FormationClass.Infantry).Shell, agent.Formation);
                Assert.True(registry.TryRegisterAgent("owner", id, agent));
            }
            spawner.DrainPendingPuppets();
            Assert.Same(mission.DefenderTeam.GetFormation(FormationClass.Ranged).Shell, agent.Formation);

            broker.Publish(this, new NetworkBattleAgentFormations("battle", "former-owner",
                new[] { new BattleAgentFormationData(id, -1, 0) }));
            Assert.NotNull(agent.Formation);
            broker.Publish(this, new NetworkBattleAgentFormations("other-battle", "owner",
                new[] { new BattleAgentFormationData(id, -1, 0) }));
            Assert.NotNull(agent.Formation);
            broker.Publish(this, ProtoBuf.Serializer.DeepClone(new NetworkBattleAgentFormations("battle", "owner",
                new[] { new BattleAgentFormationData(id, -1, 0) })));
            Assert.Null(agent.Formation);
            Assert.True(registry.TryGetAgentInfo(id, out var info));
            Assert.Same(agent, info.Agent);
        });
    }

    // K6: the owner's formation 3 is its hull's formation there; here that hull is formation 1, so crew follow the ship id.
    [Fact]
    public void CrewUpdate_JoinsItsHullsLocalFormation_AndWaitsForAHullNotSpawnedYet()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        peer.Call(() =>
        {
            var mission = fixture.CreateMission(peer);
            var broker = peer.Resolve<IMessageBroker>();
            var registry = peer.Resolve<INetworkAgentRegistry>();
            var component = peer.Resolve<ICoopMissionComponent>();
            var ships = component.ShipRegistry;
            var session = Mock.Of<IBattleSession>(value => value.InstanceId == "battle" && value.OwnControllerId == "peer");
            using var spawner = new PuppetSpawner(broker, peer.ObjectManager, peer.Resolve<IPlayerManager>(),
                component, session, new CasualtyAttributionMap(),
                Mock.Of<IBattleDeploymentCoordinator>(), new AgentFormationAssigner(), new BattleAgentBudget(),
                peer.Resolve<IMissionWeaponDataMapper>());
            var spawned = RegisterHull(ships, mission.DefenderTeam, FormationClass.Ranged);
            var late = Guid.NewGuid();
            var id = Guid.NewGuid();
            var agent = mission.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                .Controller(AgentControllerType.None).Team(mission.DefenderTeam.Shell));
            var original = mission.DefenderTeam.GetFormation(FormationClass.Infantry).Shell;
            agent.Formation = original;
            Assert.True(registry.TryRegisterAgent("owner", id, agent));

            broker.Publish(this, ProtoBuf.Serializer.DeepClone(new NetworkBattleAgentFormations("battle", "owner",
                new[] { new BattleAgentFormationData(id, (int)FormationClass.HorseArcher, 0, spawned) })));
            Assert.Same(mission.DefenderTeam.GetFormation(FormationClass.Ranged).Shell, agent.Formation);

            broker.Publish(this, new NetworkBattleAgentFormations("battle", "owner",
                new[] { new BattleAgentFormationData(id, (int)FormationClass.Infantry, 0, late) }));
            spawner.DrainPendingPuppets();
            Assert.Same(mission.DefenderTeam.GetFormation(FormationClass.Ranged).Shell, agent.Formation);

            RegisterHull(ships, mission.DefenderTeam, FormationClass.Cavalry, late);
            spawner.DrainPendingPuppets();
            Assert.Same(mission.DefenderTeam.GetFormation(FormationClass.Cavalry).Shell, agent.Formation);
        });
    }

    // K6: a catch-up snapshot carries the owner's formation index too, and refreshes crew onto its hull here.
    [Fact]
    public void CatchUp_RefreshesCrewOntoItsHullsLocalFormation()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        peer.Call(() =>
        {
            var mission = fixture.CreateMission(peer);
            var broker = peer.Resolve<IMessageBroker>();
            var registry = peer.Resolve<INetworkAgentRegistry>();
            var session = Mock.Of<IBattleSession>(value => value.InstanceId == "battle" && value.OwnControllerId == "peer");
            Mock.Get(session).Setup(value => value.IsOwn(It.IsAny<string>())).Returns((string id) => id == "peer");
            var component = peer.Resolve<ICoopMissionComponent>();
            using var spawner = new PuppetSpawner(broker, peer.ObjectManager, peer.Resolve<IPlayerManager>(),
                component, session, new CasualtyAttributionMap(),
                Mock.Of<IBattleDeploymentCoordinator>(), new AgentFormationAssigner(), new BattleAgentBudget(),
                peer.Resolve<IMissionWeaponDataMapper>());
            var shipId = RegisterHull(component.ShipRegistry, mission.DefenderTeam, FormationClass.Ranged);
            var id = Guid.NewGuid();
            var agent = mission.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                .Controller(AgentControllerType.None).Team(mission.DefenderTeam.Shell));
            agent.Formation = mission.DefenderTeam.GetFormation(FormationClass.Infantry).Shell;
            Assert.True(registry.TryRegisterAgent("owner", "owner", "owner", id, 0, agent, 0));
            var record = new BattleAgentSpawnData(id, "troop", default, BattleSideEnum.Defender,
                100f, "owner", null, 0, new Equipment(), default, null, formationIndex: (int)FormationClass.HorseArcher,
                originalOwnerControllerId: "owner", movementScopeId: "owner", shipId: shipId);

            broker.Publish(this, new NetworkSpawnBattleAgents(new[] { record }));

            Assert.Same(mission.DefenderTeam.GetFormation(FormationClass.Ranged).Shell, agent.Formation);
        });
    }

    [Fact]
    public void FormationUpdate_CarriesTheHullTheAgentCrews()
    {
        using var fixture = new MissionEngineFixture();
        var owner = Clients.First();
        owner.Call(() =>
        {
            var mission = fixture.CreateMission(owner);
            var broker = owner.Resolve<IMessageBroker>();
            var registry = owner.Resolve<INetworkAgentRegistry>();
            var network = new Mock<IBattleNetwork>();
            var session = Mock.Of<IBattleSession>(value => value.OwnControllerId == "owner" && value.InstanceId == "battle");
            var component = owner.Resolve<ICoopMissionComponent>();
            using var replicator = new OwnedAgentReplicator(network.Object, broker, owner.ObjectManager,
                component, session, new CasualtyAttributionMap(),
                Mock.Of<IBattleDeploymentCoordinator>(), new BattleAgentSpawnBatchCodec(), owner.Resolve<IMissionWeaponDataMapper>());
            var shipId = RegisterHull(component.ShipRegistry, mission.DefenderTeam, FormationClass.Ranged);
            var crew = mission.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                .Controller(AgentControllerType.AI).Team(mission.DefenderTeam.Shell));
            var swimmer = mission.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                .Controller(AgentControllerType.AI).Team(mission.DefenderTeam.Shell));
            Assert.True(registry.TryRegisterAgent("owner", Guid.NewGuid(), crew));
            Assert.True(registry.TryRegisterAgent("owner", Guid.NewGuid(), swimmer));

            crew.Formation = mission.DefenderTeam.GetFormation(FormationClass.Ranged).Shell;
            swimmer.Formation = null;
            broker.Publish(this, new BattleAgentFormationChanged(crew));
            broker.Publish(this, new BattleAgentFormationChanged(swimmer));
            replicator.FlushPendingFormations();

            var updates = network.Invocations.Select(call => call.Arguments[0]).OfType<NetworkBattleAgentFormations>()
                .SelectMany(batch => batch.Agents).ToArray();
            Assert.Equal(shipId, updates.Single(update => update.FormationIndex == (int)FormationClass.Ranged).ShipId);
            Assert.Equal(Guid.Empty, updates.Single(update => update.FormationIndex == -1).ShipId);
        });
    }

    // A hull registered on the team's formation, carrying that formation's index as a live formation does.
    private static Guid RegisterHull(INetworkShipRegistry ships, MockTeam team, FormationClass formationClass, Guid shipId = default)
    {
        var formation = team.GetFormation(formationClass).Shell;
        AccessTools.Field(typeof(Formation), nameof(Formation.FormationIndex)).SetValue(formation, formationClass);
        if (shipId == Guid.Empty) shipId = Guid.NewGuid();
        Assert.True(ships.TryRegister(new NetworkShipInfo(shipId, "owner", null, false, MissionTestObjects.CreateHull(), formation)));
        return shipId;
    }

    [Fact]
    public void NewAuthorityUpdate_WaitsForHandoffAndRejectsOlderRevision()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        peer.Call(() =>
        {
            var mission = fixture.CreateMission(peer);
            var broker = peer.Resolve<IMessageBroker>();
            var registry = peer.Resolve<INetworkAgentRegistry>();
            var session = Mock.Of<IBattleSession>(value => value.InstanceId == "battle" && value.OwnControllerId == "peer");
            using var spawner = new PuppetSpawner(broker, peer.ObjectManager, peer.Resolve<IPlayerManager>(),
                peer.Resolve<ICoopMissionComponent>(), session, new CasualtyAttributionMap(),
                Mock.Of<IBattleDeploymentCoordinator>(), new AgentFormationAssigner(), new BattleAgentBudget(),
                peer.Resolve<IMissionWeaponDataMapper>());
            var id = Guid.NewGuid();
            var agent = mission.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
                .Controller(AgentControllerType.None).Team(mission.DefenderTeam.Shell));
            var original = mission.DefenderTeam.GetFormation(FormationClass.Infantry).Shell;
            agent.Formation = original;
            Assert.True(registry.TryRegisterAgent("former", id, agent));

            broker.Publish(this, new NetworkBattleAgentFormations("battle", "successor",
                new[] { new BattleAgentFormationData(id, (int)FormationClass.Ranged, 1) }));
            broker.Publish(this, new NetworkBattleAgentFormations("battle", "former",
                new[] { new BattleAgentFormationData(id, -1, 0) }));
            Assert.Same(original, agent.Formation);
            Assert.True(registry.TryTransferAuthority("successor", id));
            spawner.DrainPendingPuppets();
            Assert.Same(mission.DefenderTeam.GetFormation(FormationClass.Ranged).Shell, agent.Formation);
            broker.Publish(this, new NetworkBattleAgentFormations("battle", "former",
                new[] { new BattleAgentFormationData(id, -1, 0) }));
            Assert.NotNull(agent.Formation);
        });
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Withdrawal_DiscardsFormationUpdatesButKeepsDeferredSpawns(bool wasHost, bool disconnected)
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, partyIds) = SetupCoopBattle("A", "B", "C");
        var peer = Clients.First();
        var characterId = CreateRegisteredObject<CharacterObject>();
        peer.Call(() =>
        {
            var mission = fixture.CreateMission(peer);
            var broker = peer.Resolve<IMessageBroker>();
            var registry = peer.Resolve<INetworkAgentRegistry>();
            var session = Mock.Of<IBattleSession>(value => value.InstanceId == mapEventId);
            Mock.Get(session).Setup(value => value.IsHostController("A")).Returns(wasHost);
            var budget = new Mock<IBattleAgentBudget>();
            budget.Setup(value => value.SlotsForEquipment(It.IsAny<Equipment>())).Returns(1);
            using var spawner = new PuppetSpawner(broker, peer.ObjectManager, peer.Resolve<IPlayerManager>(),
                peer.Resolve<ICoopMissionComponent>(), session, new CasualtyAttributionMap(),
                Mock.Of<IBattleDeploymentCoordinator>(), new AgentFormationAssigner(), budget.Object,
                peer.Resolve<IMissionWeaponDataMapper>());
            var pending = (IDictionary)AccessTools.Field(typeof(PuppetSpawner), "pendingFormations").GetValue(spawner);
            var spawns = (List<BattleAgentSpawnData>)AccessTools.Field(typeof(PuppetSpawner), "pendingPuppets").GetValue(spawner);

            BattleAgentSpawnData Record(int partyIndex, string owner)
            {
                Assert.True(peer.ObjectManager.TryGetObject<MobileParty>(partyIds[partyIndex], out var party));
                var side = party.Party.Side;
                var mapEventParty = party.MapEvent.GetMapEventSide(side).Parties.Single(value => value.Party == party.Party);
                Assert.True(peer.ObjectManager.TryGetId(mapEventParty, out var partyId));
                return new BattleAgentSpawnData(Guid.NewGuid(), characterId, default, side,
                    100f, owner, partyId, partyIndex + 1, new Equipment(), new BodyProperties(), new(new()));
            }
            void Update(BattleAgentSpawnData record)
                => broker.Publish(this, new NetworkBattleAgentFormations(mapEventId, record.OwnerControllerId,
                    new[] { new BattleAgentFormationData(record.AgentId, (int)FormationClass.Ranged, 0) }));

            var withdrawn = Record(0, "A");
            var delayed = Record(1, "B");
            var npc = Record(2, "A");
            broker.Publish(this, new NetworkSpawnBattleAgents(new[] { withdrawn, delayed, npc }));
            Update(withdrawn);
            Update(delayed);
            Update(npc);
            Assert.Equal(3, pending.Count);
            Assert.Equal(3, spawns.Count);

            if (disconnected) broker.Publish(this, new MissionPeerDisconnected("A", mapEventId));
            else broker.Publish(this, new MissionPeerLeft("A", mapEventId));

            Assert.Equal(disconnected, pending.Contains(withdrawn.AgentId));
            Assert.Equal(disconnected, spawns.Any(record => record.AgentId == withdrawn.AgentId));
            Assert.True(pending.Contains(delayed.AgentId));
            Assert.Equal(wasHost || disconnected, pending.Contains(npc.AgentId));
            Update(withdrawn);
            Assert.Equal(disconnected, pending.Contains(withdrawn.AgentId));

            // Late spawns remain valid after disconnect, but are discarded after withdrawal.
            var late = Record(0, "A");
            Update(late);
            broker.Publish(this, new NetworkSpawnBattleAgents(new[] { late }));
            Update(late);
            Assert.Equal(disconnected, pending.Contains(late.AgentId));
            spawner.DrainPendingPuppets();
            Assert.Equal(disconnected ? 4 : wasHost ? 2 : 1, pending.Count);

            budget.Setup(value => value.RemainingCapacity(It.IsAny<int>())).Returns(10);
            spawner.DrainPendingPuppets();
            Assert.Empty(pending);
            Assert.Empty(spawns);
            Assert.Equal(disconnected, registry.TryGetAgentInfo(withdrawn.AgentId, out _));
            Assert.Equal(disconnected, registry.TryGetAgentInfo(late.AgentId, out _));
            Assert.True(registry.TryGetAgentInfo(delayed.AgentId, out var delayedInfo));
            Assert.Same(mission.DefenderTeam.GetFormation(FormationClass.Ranged).Shell, delayedInfo.Agent.Formation);
            Assert.Equal(wasHost || disconnected, registry.TryGetAgentInfo(npc.AgentId, out var npcInfo));
            if (wasHost || disconnected)
                Assert.Same(mission.AttackerTeam.GetFormation(FormationClass.Ranged).Shell, npcInfo.Agent.Formation);
        });
    }
}
