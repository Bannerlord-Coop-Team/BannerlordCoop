using Common.Messaging;
using Common.Util;
using HarmonyLib;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using Missions;
using Missions.Battles;
using Missions.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

public class RetainedPlayerHeroBootstrapTests : MissionTestEnvironment
{
    public RetainedPlayerHeroBootstrapTests(ITestOutputHelper output) : base(output) { }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AcceptedGrantBuildsOneInitialPlayerBeforeSetupEvenWhenHostChanges(bool catchUpFirst)
    {
        using var fixture = new MissionEngineFixture();
        var (battleId, partyIds) = SetupCoopBattle("holder", "returner");
        var returner = Clients.Last();
        returner.Call(() =>
        {
            var mission = fixture.CreateMission(returner);
            mission.DeploymentInProgress = true;
            mission.TrackInitialPlayerAgent = true;
            mission.PlayerTeam = mission.DefenderTeam;
            var players = returner.Resolve<IPlayerManager>();
            Assert.True(players.TryGetPlayer("returner", out var player));
            Assert.True(returner.ObjectManager.TryGetObject<Hero>(player.HeroId, out var hero));
            Assert.True(returner.ObjectManager.TryGetId(hero.CharacterObject, out var characterId));
            players.RemovePlayer(player);
            Assert.True(players.AddPlayer(new Player("returner", player.HeroId, partyIds[1], player.ClanId, characterId)));
            Game.Current.PlayerTroop = hero.CharacterObject;
            Assert.Same(hero, Hero.MainHero);
            Assert.True(returner.ObjectManager.TryGetObject<MobileParty>(partyIds[1], out var party));
            var eventParty = party.MapEvent.DefenderSide.Parties.Single(value => value.Party == party.Party);
            Assert.True(returner.ObjectManager.TryGetId(eventParty, out var eventPartyId));
            var supplier = new CoopTroopSupplier(battleId, BattleSideEnum.Defender,
                returner.ObjectManager, new BattleAgentBudget());
            supplier.SetReserve(new[] { new PartyReserve(eventPartyId, 1,
                new[] { new TroopReserveEntry(1141, characterId, 0) }, isReceiverPlayerParty: true) }, 1, 1, 1000, 8);
            var enemySupplier = new CoopTroopSupplier(battleId, BattleSideEnum.Attacker,
                returner.ObjectManager, new BattleAgentBudget());
            var spawnHandler = new CoopBattleMissionSpawnHandler(supplier, enemySupplier, returner.Resolve<IMessageBroker>(), BattleSideEnum.Defender);
            var controller = returner.Resolve<CoopBattleController>();
            Assert.True(controller.Session.TryBegin(battleId));
            returner.Resolve<IBattleHostRegistry>().Set(battleId,
                new BattleHostAssignment("holder", new[] { "returner" }, 1));
            typeof(Mission).GetField("<MissionBehaviors>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(mission.Shell, new List<MissionBehavior> { controller, spawnHandler, mission.DeploymentController });
            var spawner = (IPuppetSpawner)typeof(CoopBattleController)
                .GetField("puppetSpawner", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
            var previous = new BattleAgentSpawnData(Guid.NewGuid(), characterId, default, BattleSideEnum.Defender,
                22, "holder", eventPartyId, 1141, new Equipment(), default, null, movementId: 7,
                originalOwnerControllerId: "returner", movementScopeId: "returner:old", authorityRevision: 1);
            var grant = new NetworkRetainedPlayerHero(battleId, 1, "returner", previous);
            Assert.Same(controller, Mission.Current.GetMissionBehavior<CoopBattleController>());
            Assert.Same(spawnHandler, Mission.Current.GetMissionBehavior<CoopBattleMissionSpawnHandler>());
            Assert.Equal("returner", controller.Session.OwnControllerId);
            Assert.True(spawnHandler.HasSuppliedPlayerOrigin(previous));
            var migrator = (IBattleAuthorityMigrator)typeof(CoopBattleController)
                .GetField("authorityMigrator", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
            Assert.True(migrator.IsCurrentPlayerHandoff(grant));
            var broker = returner.Resolve<IMessageBroker>();
            if (catchUpFirst) broker.Publish(this, new NetworkSpawnBattleAgents(new[] { previous }));
            Assert.Empty(mission.Agents);
            Assert.Null(Mission.Current.InitialPlayerAgent);
            broker.Publish(this, grant);
#if DEBUG
            var observation = spawner.CaptureReturningHeroCatchUpState("returner");
            Assert.Empty(observation.DiagnosticErrors);
            var captured = observation.FirstCatchUpBeforeDefer;
            Assert.NotNull(captured);
            Assert.Equal(previous.AgentId.ToString("D"), captured.AgentId);
            Assert.Equal(battleId, captured.BattleInstanceId);
            Assert.Equal(eventPartyId, captured.MapEventPartyId);
            Assert.Equal(1141, captured.TroopSeed);
            Assert.Equal("first-returning-hero-catch-up-before-defer", captured.Phase);
            Assert.Equal("holder", captured.OwnerControllerId);
            Assert.Equal("returner", captured.OriginalOwnerControllerId);
            Assert.True(captured.OriginalOwnerIsLocal);
            Assert.False(captured.CurrentOwnerIsLocal);
            Assert.True(captured.ReturningHeroIdentityMatched);
            Assert.Equal(System.Diagnostics.Process.GetCurrentProcess().Id, captured.ProcessId);
#endif
            broker.Publish(this, new NetworkBattleHostAssigned(battleId, "successor", new[] { "returner" }, 2));
            if (!catchUpFirst) broker.Publish(this, new NetworkSpawnBattleAgents(new[] { previous }));
            spawner.DrainPendingPuppets();
            Assert.Single(mission.Agents);
            Assert.Equal(1, mission.InitialPlayerBuildCount);
            Assert.Same(Mission.Current.InitialPlayerAgent, Mission.Current.MainAgent);
            Assert.True(controller.HasRetainedPlayerAgent(Mission.Current.InitialPlayerAgent));
            Assert.False(spawnHandler.IsSized);
            Assert.False(mission.DeploymentController.TeamSetupOver);
            Assert.False(controller.Deployment.IsCommitted);
            Assert.Equal(AgentControllerType.None, Mission.Current.InitialPlayerAgent.Controller);
            Assert.True(returner.Resolve<INetworkAgentRegistry>().TryGetAgentInfo(previous.AgentId, out var info));
            Assert.Equal("returner", info.CurrentAuthority);
            Assert.Equal(2, info.AuthorityRevision);
            Assert.Equal(22, info.Agent.Health);
            Assert.Equal(0, supplier.GetRemainingForParty(eventPartyId));
            Assert.Equal(1, supplier.CaptureAllocationSnapshot().SuppliedTroops);
            // Exercise the actual post-Init clamp; native scene/plan initialization is outside this fixture.
            var spawnLogic = ObjectHelper.SkipConstructor<DefaultBattleMissionAgentSpawnLogic>();
            var phase = new MissionSpawnPhase { TotalSpawnNumber = 1, InitialSpawnNumber = 1 };
            AccessTools.Field(typeof(DefaultBattleMissionAgentSpawnLogic), "_phases").SetValue(spawnLogic,
                new[] { new List<MissionSpawnPhase> { phase }, new List<MissionSpawnPhase>() });
            AccessTools.Field(typeof(CoopBattleMissionSpawnHandler), "_missionAgentSpawnLogic").SetValue(spawnHandler, spawnLogic);
            AccessTools.Method(typeof(CoopBattleMissionSpawnHandler), "ClampPhasesToOwnedShare")
                .Invoke(spawnHandler, new object[] { BattleSideEnum.Defender, supplier });
            Assert.Equal(0, phase.InitialSpawnNumber);
            Assert.Equal(0, phase.TotalSpawnNumber);
            Assert.Equal(0, phase.RemainingSpawnNumber);
            Assert.Empty(supplier.SupplyTroops(phase.InitialSpawnNumber));
            Assert.False(spawnHandler.IsSized);
            Assert.False(mission.DeploymentController.TeamSetupOver);
            Assert.False(controller.Deployment.IsCommitted);
            Assert.Equal("returner", info.CurrentAuthority);
            Assert.Equal(2, info.AuthorityRevision);
            Assert.Equal(1, supplier.CaptureAllocationSnapshot().SuppliedTroops);

            broker.Publish(this, grant);
            broker.Publish(this, new NetworkSpawnBattleAgents(new[] { previous }));
            spawner.DrainPendingPuppets();
            Assert.Single(mission.Agents);
            Assert.Equal(1, mission.InitialPlayerBuildCount);
        });
    }
}
