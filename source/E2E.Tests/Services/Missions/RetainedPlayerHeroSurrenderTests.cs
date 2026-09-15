using Common.Messaging;
using Common.Network;
using E2E.Tests.Environment;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using Missions;
using Missions.Battles;
using Missions.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

public class RetainedPlayerHeroSurrenderTests : MissionTestEnvironment
{
    public RetainedPlayerHeroSurrenderTests(ITestOutputHelper output) : base(output, numClients: 3) { }

    [Fact]
    public void RejectedSurrenderKeepsExistingObserverReadyForSecondRejoin()
    {
        using var fixture = new MissionEngineFixture();
        var (battleId, partyIds) = SetupCoopBattle("holder", "returner", "observer");
        var clients = Clients.ToArray();
        var agentId = Guid.NewGuid();
        BattleAgentSpawnData previous = null;
        var agents = new Agent[3];
        var migrators = new IBattleAuthorityMigrator[3];
        Server.Call(() =>
        {
            var players = Server.Resolve<IPlayerManager>();
            for (int i = 0; i < clients.Length; i++)
                players.SetPeer(new[] { "holder", "returner", "observer" }[i], clients[i].NetPeer);
            Assert.True(players.TryGetPlayer("returner", out var player));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(player.HeroId, out var hero));
            Assert.True(Server.ObjectManager.TryGetId(hero.CharacterObject, out var characterId));
            players.RemovePlayer(player);
            Assert.True(players.AddPlayer(new Player("returner", player.HeroId, partyIds[1], player.ClanId, characterId)));
            players.SetPeer("returner", clients[1].NetPeer);
        });
        foreach (var client in clients) EnterBattle(client, battleId);
        for (int index = 0; index < clients.Length; index++)
        {
            int i = index;
            var client = clients[i];
            client.Call(() =>
            {
                var mission = fixture.CreateMission(client);
                var players = client.Resolve<IPlayerManager>();
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
                AccessTools.Field(typeof(Mission), "<MissionBehaviors>k__BackingField").SetValue(mission.Shell,
                    new List<MissionBehavior> { controller });
                migrators[i] = (IBattleAuthorityMigrator)AccessTools.Field(typeof(CoopBattleController), "authorityMigrator")
                    .GetValue(controller);
                var broker = client.Resolve<IMessageBroker>();
                foreach (var id in new[] { "holder", "returner", "observer" })
                    broker.Publish(this, new NetworkMissionPeerEntered(id, battleId));
                agents[i] = mission.SpawnAgent(new AgentBuildData(hero.CharacterObject)
                    .Controller(i == 0 ? AgentControllerType.AI : AgentControllerType.None)
                    .Team(mission.DefenderTeam.Shell).Equipment(new Equipment()));
                agents[i].Health = 22;
                Assert.True(client.Resolve<INetworkAgentRegistry>().TryRegisterAgent(
                    "holder", "returner", "returner:first-mission", agentId, 7, agents[i], 1));
                previous = new BattleAgentSpawnData(agentId, characterId, default, BattleSideEnum.Defender,
                    22, "holder", eventPartyId, 1141, new Equipment(), default, null, movementId: 7,
                    originalOwnerControllerId: "returner", movementScopeId: "returner:first-mission", authorityRevision: 1);
            });
        }
        Server.Call(() =>
        {
            var ledger = Server.Resolve<IBattleTroopLedger>();
            ledger.SetReserve(battleId, previous.MapEventPartyId,
                new[] { new TroopReserveEntry(1141, previous.CharacterId, 0) });
            ledger.ReportSupplied(battleId, previous.MapEventPartyId, 1);
        });
        var router = Server.Resolve<TestNetworkRouter>();
        router.AutoDrainReady = false;
        clients[0].Call(() =>
        {
            Assert.True(migrators[0].TrySurrenderPlayerHero("returner", previous));
            Assert.Equal(AgentControllerType.None, agents[0].Controller);
            Assert.True(clients[0].Resolve<INetworkAgentRegistry>().TryGetAgentInfo(agentId, out var info));
            Assert.Equal("holder", info.CurrentAuthority);
            Assert.Equal(1, info.AuthorityRevision);
        });
        // The server observes the departure before the queued holder request is accepted.
        DepartBattle("returner", battleId);
        router.DrainReady();
        Assert.Empty(clients[2].InternalMessages.GetMessages<NetworkRetainedPlayerHero>());
        foreach (var client in clients)
            client.Call(() => client.Resolve<IMessageBroker>().Publish(this, new MissionPeerDisconnected("returner", battleId)));
        for (int i = 0; i < clients.Length; i++)
        {
            int index = i;
            clients[i].Call(() =>
            {
                Assert.True(clients[index].Resolve<INetworkAgentRegistry>().TryGetAgentInfo(agentId, out var info));
                Assert.Equal("holder", info.CurrentAuthority);
                Assert.Equal(1, info.AuthorityRevision);
                Assert.Equal(index == 0 ? AgentControllerType.AI : AgentControllerType.None, info.Agent.Controller);
            });
        }
        router.AutoDrainReady = true;
        EnterBattle(clients[1], battleId);
        foreach (var client in clients)
            client.Call(() => client.Resolve<IMessageBroker>().Publish(this, new NetworkMissionPeerEntered("returner", battleId)));
        clients[0].Call(() => Assert.True(migrators[0].TrySurrenderPlayerHero("returner", previous)));
        Assert.Single(clients[2].InternalMessages.GetMessages<NetworkRetainedPlayerHero>());
        for (int i = 0; i < clients.Length; i++)
        {
            int index = i;
            clients[i].Call(() =>
            {
                var registry = clients[index].Resolve<INetworkAgentRegistry>();
                Assert.True(registry.TryGetAgentInfo(agentId, out var info));
                Assert.Equal("returner", info.CurrentAuthority);
                Assert.Equal(2, info.AuthorityRevision);
                Assert.Same(agents[index], info.Agent);
                Assert.Single(registry.GetAgents("returner"));
                Assert.Equal("returner", info.OriginalOwner);
                Assert.Equal("returner:first-mission", info.MovementScopeId);
                Assert.Equal(7, info.MovementId);
                Assert.Equal(22, info.Agent.Health);
            });
        }
        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IBattleTroopLedger>().TryGetReserve(battleId,
                previous.MapEventPartyId, out var entries, out int supplied));
            Assert.Single(entries);
            Assert.Equal(1, supplied);
        });
    }
}
