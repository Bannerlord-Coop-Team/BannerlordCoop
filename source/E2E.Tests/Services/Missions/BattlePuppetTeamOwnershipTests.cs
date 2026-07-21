using System;
using System.Linq;
using Common.Messaging;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.MapEvents;
using HarmonyLib;
using Missions;
using Missions.Battles;
using Missions.Data;
using Missions.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

public class BattlePuppetTeamOwnershipTests : MissionTestEnvironment
{
    public BattlePuppetTeamOwnershipTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    [Trait("Requirement", "BR-022")]
    public void AlliedPlayerJoinsHost_RemotePartyStaysOffHostsPlayerTeam()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, partyIds) = SetupCoopBattle("host", "enemy", "ally");
        var host = Clients.First();
        var ownAgentId = Guid.NewGuid();
        var alliedAgentId = Guid.NewGuid();
        var characterId = CreateRegisteredObject<CharacterObject>();

        const string harmonyId = "e2e.coop-player-ally-team";
        var harmony = new Harmony(harmonyId);
        try
        {
            var patchType = typeof(BattleSpawnGate).Assembly.GetType(
                "GameInterface.Services.MapEvents.Patches.CoopPlayerAllyTeamPatch");
            Assert.NotNull(patchType);
            harmony.CreateClassProcessor(patchType).Patch();

            host.Call(() =>
            {
                var mock = fixture.CreateMission(host);
                Assert.True(host.ObjectManager.TryGetObject<MobileParty>(partyIds[0], out var ownParty));
                Assert.True(host.ObjectManager.TryGetObject<MobileParty>(partyIds[1], out var enemyParty));
                Assert.True(host.ObjectManager.TryGetObject<MobileParty>(partyIds[2], out var alliedParty));

                mock.PlayerTeam = mock.AttackerTeam;
                var controller = host.Resolve<CoopBattleController>();
                var registry = host.Resolve<INetworkAgentRegistry>();
                controller.Session.TryBegin(mapEventId);
                host.Resolve<IBattleHostRegistry>().Set(
                    mapEventId,
                    new BattleHostAssignment("host", new[] { "enemy", "ally" }));
                BattleSpawnGate.BeginBattle(mapEventId, 1000);

                var mapEventParties = ownParty.MapEvent.AttackerSide.Parties;
                var ownMapEventParty = mapEventParties.Single(party => party.Party == ownParty.Party);
                var alliedMapEventParty = mapEventParties.Single(party => party.Party == alliedParty.Party);
                Assert.True(host.ObjectManager.TryGetId(ownMapEventParty, out var ownMapEventPartyId));
                Assert.True(host.ObjectManager.TryGetId(alliedMapEventParty, out var alliedMapEventPartyId));

                var equipment = new Equipment();
                var missionEquipment = new MissionEquipmentData(new());
                var ownRecord = new BattleAgentSpawnData(
                    ownAgentId, characterId, default, BattleSideEnum.Attacker, 100f,
                    "host", ownMapEventPartyId, 1, equipment, new BodyProperties(), missionEquipment);
                var alliedRecord = new BattleAgentSpawnData(
                    alliedAgentId, characterId, default, BattleSideEnum.Attacker, 100f,
                    "ally", alliedMapEventPartyId, 2, equipment, new BodyProperties(), missionEquipment);

                var broker = host.Resolve<IMessageBroker>();
                broker.Publish(this, new NetworkSpawnBattleAgents(new[] { alliedRecord }));
                // The unsafe fallback used to spawn this record directly onto PlayerTeam.
                Assert.False(registry.TryGetAgentInfo(alliedAgentId, out _));
                Assert.Empty(mock.Agents);

                var combatants = new IBattleCombatant[] { ownParty.Party, enemyParty.Party };
                var combatantsLogic = new MissionCombatantsLogic(
                    combatants,
                    ownParty.Party,
                    enemyParty.Party,
                    ownParty.Party,
                    Mission.MissionTeamAITypeEnum.FieldBattle,
                    isPlayerSergeant: false)
                {
                    Mission = mock.Shell,
                };
                combatantsLogic.OnBehaviorInitialize();

                Assert.Same(mock.AttackerTeam, mock.PlayerTeam);
                Assert.NotNull(mock.AttackerAllyTeam);

                broker.Publish(this, new NetworkSpawnBattleAgents(new[] { ownRecord, alliedRecord }));

                Assert.True(registry.TryGetAgentInfo(ownAgentId, out var ownAgent));
                Assert.True(registry.TryGetAgentInfo(alliedAgentId, out var alliedAgent));
                Assert.True(AgentMirror.TryGet(ownAgent.Agent, out var ownMirror));
                Assert.True(AgentMirror.TryGet(alliedAgent.Agent, out var alliedMirror));

                Assert.Same(mock.PlayerTeam.Shell, ownMirror.Team);
                Assert.Same(mock.AttackerAllyTeam.Shell, alliedMirror.Team);
                Assert.NotSame(mock.PlayerTeam.Shell, alliedMirror.Team);
                Assert.Equal(AgentControllerType.AI, ownMirror.Controller);
                Assert.Equal(AgentControllerType.None, alliedMirror.Controller);
                Assert.True(registry.IsLocallyControlled(ownAgentId));
                Assert.False(registry.IsLocallyControlled(alliedAgentId));
                Assert.Equal("ally", alliedAgent.CurrentAuthority);

                GC.KeepAlive(controller);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
            BattleSpawnGate.EndBattle();
        }
    }
}
