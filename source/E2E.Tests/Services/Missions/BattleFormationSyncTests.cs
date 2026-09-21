using Common.Messaging;
using E2E.Tests.Environment.MockEngine;
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

            Assert.False(pending.Contains(withdrawn.AgentId));
            Assert.DoesNotContain(spawns, record => record.AgentId == withdrawn.AgentId);
            Assert.True(pending.Contains(delayed.AgentId));
            Assert.Equal(wasHost, pending.Contains(npc.AgentId));
            Update(withdrawn);
            Assert.False(pending.Contains(withdrawn.AgentId));

            // A stale spawn arriving after withdrawal must retire its update too.
            var late = Record(0, "A");
            Update(late);
            broker.Publish(this, new NetworkSpawnBattleAgents(new[] { late }));
            Update(late);
            Assert.False(pending.Contains(late.AgentId));
            spawner.DrainPendingPuppets();
            Assert.Equal(wasHost ? 2 : 1, pending.Count);

            budget.Setup(value => value.RemainingCapacity(It.IsAny<int>())).Returns(10);
            spawner.DrainPendingPuppets();
            Assert.Empty(pending);
            Assert.Empty(spawns);
            Assert.False(registry.TryGetAgentInfo(withdrawn.AgentId, out _));
            Assert.True(registry.TryGetAgentInfo(delayed.AgentId, out var delayedInfo));
            Assert.Same(mission.DefenderTeam.GetFormation(FormationClass.Ranged).Shell, delayedInfo.Agent.Formation);
            Assert.Equal(wasHost, registry.TryGetAgentInfo(npc.AgentId, out var npcInfo));
            if (wasHost)
                Assert.Same(mission.AttackerTeam.GetFormation(FormationClass.Ranged).Shell, npcInfo.Agent.Formation);
        });
    }
}
