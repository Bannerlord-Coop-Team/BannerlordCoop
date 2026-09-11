using Common.Messaging;
using Common.Network;
using E2E.Tests.Environment;
using E2E.Tests.Environment.MockEngine;
using HarmonyLib;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using Missions;
using Missions.Battles;
using Missions.Data;
using Missions.Messages;
using Missions.Services.Network;
using Moq;
using System;
using System.Linq;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

public class RetainedPlayerHeroRecoveryTests : MissionTestEnvironment
{
    public RetainedPlayerHeroRecoveryTests(ITestOutputHelper output) : base(output) { }

#if DEBUG
    [Theory]
    [InlineData("returner")]
    [InlineData("holder")]
    public void OrdinaryCatchUpRecordDoesNotPoisonReturningHeroObservation(string currentOwner)
    {
        using var fixture = new MissionEngineFixture();
        var (battleId, _) = SetupCoopBattle("returner", "holder");
        var returner = Clients.First();
        returner.Call(() =>
        {
            fixture.CreateMission(returner);
            var players = returner.Resolve<IPlayerManager>();
            Assert.True(players.TryGetPlayer("returner", out var player));
            players.RemovePlayer(player);
            Assert.True(players.AddPlayer(new Player("returner", player.HeroId, player.MobilePartyId,
                player.ClanId, "CharacterObject_Player")));
            var controller = returner.Resolve<CoopBattleController>();
            Assert.True(controller.Session.TryBegin(battleId));
            var spawner = (IPuppetSpawner)AccessTools.Field(typeof(CoopBattleController), "puppetSpawner")
                .GetValue(controller);
            var identify = AccessTools.Method(typeof(PuppetSpawner), "TryIdentifyReturningHeroCatchUpRecord");
            var ordinary = new BattleAgentSpawnData(Guid.NewGuid(), "ordinary-troop", default,
                BattleSideEnum.Defender, 22, currentOwner, "party", 1141, new Equipment(), default, null,
                originalOwnerControllerId: "returner");
            Assert.False((bool)identify.Invoke(spawner, new object[] { ordinary, SpawnBatchPurpose.CatchUp }));
            Assert.Empty(spawner.CaptureReturningHeroCatchUpState("returner").DiagnosticErrors);
            var hero = new BattleAgentSpawnData(Guid.NewGuid(), "CharacterObject_Player", default,
                BattleSideEnum.Defender, 22, currentOwner, "party", 1142, new Equipment(), default, null,
                originalOwnerControllerId: "returner");
            Assert.True((bool)identify.Invoke(spawner, new object[] { hero, SpawnBatchPurpose.CatchUp }));
            Assert.Empty(spawner.CaptureReturningHeroCatchUpState("returner").DiagnosticErrors);
        });
    }
#endif

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SuccessorReconstructsHandoffOnlyForStillPresentReturner(bool returnerPresent)
    {
        using var fixture = new MissionEngineFixture();
        var (battleId, partyIds) = SetupCoopBattle("successor", "returner");
        var successor = Clients.First();
        successor.Call(() =>
        {
            var mission = fixture.CreateMission(successor);
            var players = successor.Resolve<IPlayerManager>();
            Assert.True(players.TryGetPlayer("returner", out var player));
            Assert.True(successor.ObjectManager.TryGetObject<Hero>(player.HeroId, out var hero));
            Assert.True(successor.ObjectManager.TryGetId(hero.CharacterObject, out var characterId));
            players.RemovePlayer(player);
            Assert.True(players.AddPlayer(new Player("returner", player.HeroId, partyIds[1], player.ClanId, characterId)));
            Assert.True(successor.ObjectManager.TryGetObject<MobileParty>(partyIds[1], out var party));
            var eventParty = party.MapEvent.DefenderSide.Parties.Single(value => value.Party == party.Party);
            Assert.True(successor.ObjectManager.TryGetId(eventParty, out var eventPartyId));

            var registry = successor.Resolve<INetworkAgentRegistry>();
            var hosts = successor.Resolve<IBattleHostRegistry>();
            hosts.Set(battleId, new BattleHostAssignment("former", new[] { "successor", "returner" }, 1));
            var session = new BattleSession(successor.Resolve<IControllerIdProvider>(), hosts);
            Assert.True(session.TryBegin(battleId));
            var broker = successor.Resolve<IMessageBroker>();
            var relay = new Mock<INetwork>();
            var context = new Mock<IMissionContext>();
            context.SetupGet(value => value.ControllersInMission)
                .Returns(returnerPresent ? new[] { "successor", "returner" } : new[] { "successor" });
            var component = successor.Resolve<ICoopMissionComponent>();
            var casualties = new CasualtyAttributionMap();
            var deployment = Mock.Of<IBattleDeploymentCoordinator>();
            using var migrator = new BattleAuthorityMigrator(relay.Object, broker, successor.ObjectManager,
                players, component, session, casualties, deployment, Mock.Of<IAgentFormationAssigner>(),
                context.Object, Mock.Of<IReinforcementFielder>());
            using var replicator = new OwnedAgentReplicator(Mock.Of<IBattleNetwork>(), broker,
                successor.ObjectManager, component, session, casualties, deployment,
                new BattleAgentSpawnBatchCodec(), successor.Resolve<IMissionWeaponDataMapper>(), migrator);
            var agentId = Guid.NewGuid();
            var agent = mission.SpawnAgent(new AgentBuildData(hero.CharacterObject)
                .Controller(AgentControllerType.None).Team(mission.DefenderTeam.Shell).Equipment(new Equipment()));
            agent.Health = 22;
            Assert.True(registry.TryRegisterAgent("former", "returner", "returner:first-mission", agentId, 7, agent, 1));
            casualties.Record(agentId, eventPartyId, 1141, characterId);

            replicator.RecoverRetainedPlayerHandoffs(0.5f);
            Assert.DoesNotContain(relay.Invocations, call => call.Arguments[0] is NetworkRequestRetainedPlayerHero);
            hosts.Set(battleId, new BattleHostAssignment("successor", new[] { "returner" }, 2));
            broker.Publish(this, new BattleHostMigrated(battleId, "former", "successor"));
            replicator.RecoverRetainedPlayerHandoffs(0.5f);
            var requests = relay.Invocations.Select(call => call.Arguments[0]).OfType<NetworkRequestRetainedPlayerHero>().ToArray();
            Assert.Equal(returnerPresent ? 1 : 0, requests.Length);
            Assert.True(registry.TryGetAgentInfo(agentId, out var info));
            Assert.Same(agent, info.Agent);
            Assert.Single(mission.Agents);
            Assert.Equal(22, agent.Health);
            Assert.Equal("returner", info.OriginalOwner);
            Assert.Equal("returner:first-mission", info.MovementScopeId);
            Assert.Equal(7, info.MovementId);
            Assert.Equal("successor", info.CurrentAuthority);
            Assert.Equal(2, info.AuthorityRevision);
            if (!returnerPresent) return;

            var handoff = requests[0].Handoff;
            Assert.Equal(battleId, handoff.BattleInstanceId);
            Assert.Equal(2, handoff.HostEpoch);
            Assert.Equal("successor", handoff.Previous.OwnerControllerId);
            Assert.Equal(2, handoff.Previous.AuthorityRevision);
            Assert.Equal(1141, handoff.Previous.TroopSeed);
            Assert.Equal(eventPartyId, handoff.Previous.MapEventPartyId);
            Assert.Equal(AgentControllerType.None, agent.Controller);
            replicator.RecoverRetainedPlayerHandoffs(0.5f);
            migrator.TickPlayerHandoffs(0.5f);
            Assert.Equal(2, relay.Invocations.Count(call => call.Arguments[0] is NetworkRequestRetainedPlayerHero));
            Assert.True(migrator.ApplyPlayerHandoff(handoff));
            migrator.TickPlayerHandoffs(0.5f);
            Assert.Equal(2, relay.Invocations.Count(call => call.Arguments[0] is NetworkRequestRetainedPlayerHero));
            Assert.Equal(3, info.AuthorityRevision);
        });
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DisconnectedReturnerDoesNotBlockNewHostSnapshot(bool hostMigrates)
    {
        using var fixture = new MissionEngineFixture();
        var (battleId, partyIds) = SetupCoopBattle("observer", "returner");
        var observer = Clients.First();
        observer.Call(() =>
        {
            var mission = fixture.CreateMission(observer);
            mission.DeploymentInProgress = true;
            var players = observer.Resolve<IPlayerManager>();
            Assert.True(players.TryGetPlayer("returner", out var player));
            Assert.True(observer.ObjectManager.TryGetObject<Hero>(player.HeroId, out var hero));
            Assert.True(observer.ObjectManager.TryGetId(hero.CharacterObject, out var characterId));
            players.RemovePlayer(player);
            Assert.True(players.AddPlayer(new Player("returner", player.HeroId, partyIds[1], player.ClanId, characterId)));
            Assert.True(observer.ObjectManager.TryGetObject<MobileParty>(partyIds[1], out var party));
            var eventParty = party.MapEvent.DefenderSide.Parties.Single(value => value.Party == party.Party);
            Assert.True(observer.ObjectManager.TryGetId(eventParty, out var eventPartyId));
            var controller = observer.Resolve<CoopBattleController>();
            Assert.True(controller.Session.TryBegin(battleId));
            observer.Resolve<IBattleHostRegistry>().Set(battleId,
                new BattleHostAssignment("holder", new[] { "observer", "returner" }, 1));
            AccessTools.Field(typeof(Mission), "<MissionBehaviors>k__BackingField").SetValue(mission.Shell,
                new List<MissionBehavior> { controller, mission.DeploymentController });
            var spawner = (IPuppetSpawner)AccessTools.Field(typeof(CoopBattleController), "puppetSpawner")
                .GetValue(controller);
            var migrator = (IBattleAuthorityMigrator)AccessTools.Field(typeof(CoopBattleController), "authorityMigrator")
                .GetValue(controller);
            var broker = observer.Resolve<IMessageBroker>();
            broker.Publish(this, new NetworkMissionPeerEntered("holder", battleId));
            broker.Publish(this, new NetworkMissionPeerEntered("returner", battleId));
            var previous = new BattleAgentSpawnData(Guid.NewGuid(), characterId, default, BattleSideEnum.Defender,
                22, "holder", eventPartyId, 1141, new Equipment(), default, null, movementId: 7,
                originalOwnerControllerId: "returner", movementScopeId: "returner:first-mission", authorityRevision: 1);
            var grant = new NetworkRetainedPlayerHero(battleId, 1, "returner", previous);
            Assert.True(migrator.IsCurrentPlayerHandoff(grant));
            broker.Publish(this, grant);
            spawner.DrainPendingPuppets();
            Assert.Empty(mission.Agents);

            broker.Publish(this, new MissionPeerDisconnected("returner", battleId));
            Assert.False(migrator.IsPlayerHandoffIdentityValid(grant));
            if (hostMigrates)
            {
                broker.Publish(this, new MissionPeerDisconnected("holder", battleId));
                broker.Publish(this, new NetworkBattleHostAssigned(battleId, "successor", new[] { "observer" }, 2));
            }
            broker.Publish(this, grant);
            var hostSnapshot = new BattleAgentSpawnData(previous.AgentId, characterId, default, BattleSideEnum.Defender,
                17, "holder", eventPartyId, 1141, new Equipment(), default, null, movementId: 7,
                originalOwnerControllerId: "returner", movementScopeId: "returner:first-mission", authorityRevision: 3);
            broker.Publish(this, new NetworkSpawnBattleAgents(new[] { hostSnapshot }));
            spawner.DrainPendingPuppets();
            Assert.Empty(mission.Agents);
            mission.DeploymentController.TeamSetupOver = true;
            spawner.DrainPendingPuppets();

            var registry = observer.Resolve<INetworkAgentRegistry>();
            Assert.True(registry.TryGetAgentInfo(previous.AgentId, out var info));
            Assert.Single(mission.Agents);
            Assert.Equal(hostMigrates ? "successor" : "holder", info.CurrentAuthority);
            Assert.Equal(hostMigrates ? 4 : 3, info.AuthorityRevision);
            Assert.Equal("returner", info.OriginalOwner);
            Assert.Equal("returner:first-mission", info.MovementScopeId);
            Assert.Equal(7, info.MovementId);
            Assert.Equal(17, info.Agent.Health);
            Assert.False(controller.Deployment.IsCommitted);
            broker.Publish(this, new NetworkSpawnBattleAgents(new[] { hostSnapshot }));
            spawner.DrainPendingPuppets();
            Assert.Single(mission.Agents);
            Assert.True(registry.TryGetAgentInfo(previous.AgentId, out var duplicate));
            Assert.Same(info.Agent, duplicate.Agent);
            Assert.Equal(hostMigrates ? 4 : 3, duplicate.AuthorityRevision);
        });
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void OrderedGrantAndMigrationKeepBothPeersOnOneRevision(bool firstGrantAccepted, bool returnerPumpsFirst)
    {
        using var fixture = new MissionEngineFixture();
        var (battleId, partyIds) = SetupCoopBattle("successor", "returner");
        var clients = Clients.ToArray();
        var agentId = Guid.NewGuid();
        NetworkRetainedPlayerHero firstGrant = null;
        foreach (var client in clients)
        {
            client.Call(() =>
            {
                var mission = fixture.CreateMission(client);
                var players = client.Resolve<IPlayerManager>();
                Assert.True(players.TryGetPlayer(client.Resolve<IControllerIdProvider>().ControllerId, out var localPlayer));
                Assert.True(client.ObjectManager.TryGetObject<Hero>(localPlayer.HeroId, out var localHero));
                Game.Current.PlayerTroop = localHero.CharacterObject;
                Assert.Same(localHero, Hero.MainHero);
                Assert.True(players.TryGetPlayer("returner", out var player));
                Assert.True(client.ObjectManager.TryGetObject<Hero>(player.HeroId, out var hero));
                Assert.True(client.ObjectManager.TryGetId(hero.CharacterObject, out var characterId));
                players.RemovePlayer(player);
                Assert.True(players.AddPlayer(new Player("returner", player.HeroId, partyIds[1], player.ClanId, characterId)));
                Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyIds[1], out var party));
                var eventParty = party.MapEvent.DefenderSide.Parties.Single(value => value.Party == party.Party);
                Assert.True(client.ObjectManager.TryGetId(eventParty, out var eventPartyId));
                var controller = client.Resolve<CoopBattleController>();
                Assert.True(controller.Session.TryBegin(battleId));
                client.Resolve<IBattleHostRegistry>().Set(battleId,
                    new BattleHostAssignment("former", new[] { "successor", "returner" }, 1));
                AccessTools.Field(typeof(Mission), "<MissionBehaviors>k__BackingField")
                    .SetValue(mission.Shell, new List<MissionBehavior> { controller });
                var broker = client.Resolve<IMessageBroker>();
                broker.Publish(this, new NetworkMissionPeerEntered("successor", battleId));
                broker.Publish(this, new NetworkMissionPeerEntered("returner", battleId));
                var agent = mission.SpawnAgent(new AgentBuildData(hero.CharacterObject)
                    .Controller(AgentControllerType.None).Team(mission.DefenderTeam.Shell).Equipment(new Equipment()));
                agent.Health = 22;
                Assert.Equal(client == clients[1], OwnedAgentReplicator.IsOwnPartyAgent(agent, hero.CharacterObject));
                Assert.True(client.Resolve<INetworkAgentRegistry>().TryRegisterAgent(
                    "former", "returner", "returner:first-mission", agentId, 7, agent, 1));
                var replicator = (IOwnedAgentReplicator)AccessTools.Field(typeof(CoopBattleController), "replicator").GetValue(controller);
                var casualties = (CasualtyAttributionMap)AccessTools.Field(typeof(OwnedAgentReplicator), "casualties").GetValue(replicator);
                casualties.Record(agentId, eventPartyId, 1141, characterId);
                var data = new BattleAgentSpawnData(agentId, characterId, default, BattleSideEnum.Defender,
                    22, "former", eventPartyId, 1141, new Equipment(), default, null, movementId: 7,
                    originalOwnerControllerId: "returner", movementScopeId: "returner:first-mission", authorityRevision: 1);
                firstGrant = new NetworkRetainedPlayerHero(battleId, 1, "returner", data);
            });
        }

        Server.Resolve<TestNetworkRouter>().ReceiveContext = TestNetworkReceiveContext.PollerThread;
        // Model the two server outcomes: forward before migration, or refuse the departed holder's request.
        Server.Call(() =>
        {
            var network = Server.Resolve<INetwork>();
            if (firstGrantAccepted) network.SendAll(firstGrant);
            network.SendAll(new NetworkBattleHostAssigned(battleId, "successor", new[] { "returner" }, 2));
        });
        foreach (var client in clients)
        {
            Assert.True(client.PendingGameThreadActionCount > 0);
            client.Call(() =>
            {
                Assert.True(client.Resolve<INetworkAgentRegistry>().TryGetAgentInfo(agentId, out var info));
                Assert.Equal("former", info.CurrentAuthority);
                Assert.Equal(1, info.AuthorityRevision);
            });
        }
        var first = clients[returnerPumpsFirst ? 1 : 0];
        var second = clients[returnerPumpsFirst ? 0 : 1];
        first.PumpGameThread();
        second.PumpGameThread();
        foreach (var client in clients)
        {
            client.Call(() =>
            {
                Assert.True(client.Resolve<INetworkAgentRegistry>().TryGetAgentInfo(agentId, out var info));
                Assert.Equal(firstGrantAccepted ? "returner" : "successor", info.CurrentAuthority);
                Assert.Equal(2, info.AuthorityRevision);
            });
        }

        clients[0].Call(() =>
        {
            var controller = Mission.Current.GetMissionBehavior<CoopBattleController>();
            Assert.Equal(battleId, controller.Session.InstanceId);
            Assert.True(controller.Session.IsLocalHost);
            var replicator = (IOwnedAgentReplicator)AccessTools.Field(typeof(CoopBattleController), "replicator").GetValue(controller);
            replicator.RecoverRetainedPlayerHandoffs(0.5f);
        });
        var requests = clients[0].NetworkSentMessages.GetMessages<NetworkRequestRetainedPlayerHero>().ToArray();
        Assert.Equal(firstGrantAccepted ? 0 : 1, requests.Length);
        if (!firstGrantAccepted)
        {
            Assert.Equal("successor", requests[0].Handoff.Previous.OwnerControllerId);
            Assert.Equal(2, requests[0].Handoff.Previous.AuthorityRevision);
            Server.Call(() => Server.Resolve<INetwork>().SendAll(requests[0].Handoff));
            first.PumpGameThread();
            second.PumpGameThread();
        }
        foreach (var client in clients)
        {
            client.Call(() =>
            {
                var registry = client.Resolve<INetworkAgentRegistry>();
                Assert.True(registry.TryGetAgentInfo(agentId, out var info));
                Assert.Equal("returner", info.CurrentAuthority);
                Assert.Equal(firstGrantAccepted ? 2 : 3, info.AuthorityRevision);
                Assert.Equal("returner", info.OriginalOwner);
                Assert.Equal(22, info.Agent.Health);
                Assert.Single(registry.GetAgents("returner"));
            });
        }
        Server.PumpGameThread();
    }

}
