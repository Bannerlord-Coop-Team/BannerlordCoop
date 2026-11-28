using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.MapEvents.TroopSupply;
using HarmonyLib;
using Missions.Battles;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// The keep ends on the live agents every client sees: a side counts as beaten only once its agents were seen and
/// then all fell, and vanilla's end logic reads that rule instead of the native controller's own spawn count.
/// </summary>
public class CoopLordsHallFightMissionControllerTests : MissionTestEnvironment
{
    public CoopLordsHallFightMissionControllerTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void DefendersSeenThenAllFallen_EndTheKeepForTheAttackers()
    {
        using var fixture = new MissionEngineFixture();
        var client = Clients.First();
        client.Call(() =>
        {
            var mock = fixture.CreateMission(client);
            var attackerTeam = mock.AddTeam(BattleSideEnum.Attacker);
            mock.PlayerTeam = mock.AttackerTeam;
            var defenderTeam = mock.AddTeam(BattleSideEnum.Defender);
            var controller = new CoopLordsHallFightMissionController(
                new CoopTroopSupplier("keep", BattleSideEnum.Defender, null, null),
                new CoopTroopSupplier("keep", BattleSideEnum.Attacker, null, null),
                BattleSideEnum.Attacker,
                new DefaultSiegeLordsHallFightModel());
            AccessTools.Property(typeof(MissionBehavior), nameof(MissionBehavior.Mission)).SetValue(controller, mock.Shell);
            controller._isMissionInitialized = true;
            mock.Shell.MissionBehaviors.Add(controller);
            IMissionAgentSpawnLogic spawnLogic = controller;

            // The host fields the defenders, so before they reach this client they aren't beaten.
            mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop).Team(attackerTeam).Controller(AgentControllerType.Player));
            Assert.False(spawnLogic.IsSideDepleted(BattleSideEnum.Defender));

            var guards = new[]
            {
                mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop).Team(defenderTeam).Controller(AgentControllerType.AI)),
                mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop).Team(defenderTeam).Controller(AgentControllerType.AI)),
            };
            Assert.False(spawnLogic.IsSideDepleted(BattleSideEnum.Defender));
            Assert.True(AgentMirror.TryGet(guards[0], out var first));
            first.IsActive = false;
            Assert.False(spawnLogic.IsSideDepleted(BattleSideEnum.Defender));
            Assert.True(AgentMirror.TryGet(guards[1], out var last));
            last.IsActive = false;

            Assert.True(spawnLogic.IsSideDepleted(BattleSideEnum.Defender));
            Assert.False(spawnLogic.IsSideDepleted(BattleSideEnum.Attacker));
            AssertBattleEndReadsTheDefendersBeaten(spawnLogic, mock.Shell);
        }, new[] { AccessTools.Method(typeof(MBCommon), nameof(MBCommon.GetTotalMissionTime)) });
    }

    // Native sizes the first attacker wave from the defenders this client spawned, and a peer spawns none of the
    // host's, so the follow-up spawn has to field the rest of the chosen squad.
    [Fact]
    public void NonHostClient_SpawnsItsWholeChosenSquadWithoutLocalDefenders()
    {
        const int squad = 6;
        using var fixture = new MissionEngineFixture();
        var client = Clients.First();
        var characterId = CreateRegisteredObject<CharacterObject>();
        client.Call(() =>
        {
            var mock = fixture.CreateMission(client);
            var attackerTeam = mock.AddTeam(BattleSideEnum.Attacker);
            mock.PlayerTeam = mock.AttackerTeam;
            mock.AddTeam(BattleSideEnum.Defender);
            mock.SpawnTroopsOnTeams = true;
            var attacker = new CoopTroopSupplier("keep", BattleSideEnum.Attacker, client.ObjectManager, null);
            var defender = new CoopTroopSupplier("keep", BattleSideEnum.Defender, client.ObjectManager, null);
            attacker.SetReserve(new[] { new PartyReserve("squad", 0, Enumerable.Range(1, squad)
                .Select(seed => new TroopReserveEntry(seed, characterId, 0)).ToArray()) }, squad, 0, 1000);
            defender.SetReserve(Array.Empty<PartyReserve>(), 27, 0, 1000);
            var controller = new CoopLordsHallFightMissionController(defender, attacker, BattleSideEnum.Attacker,
                new DefaultSiegeLordsHallFightModel());
            AccessTools.Property(typeof(MissionBehavior), nameof(MissionBehavior.Mission)).SetValue(controller, mock.Shell);
            controller._isMissionInitialized = true;
            controller._areaIndexList = new List<int>();

            controller.OnMissionTick(0.1f);

            Assert.Equal(squad, mock.Agents.Count(agent =>
                AgentMirror.TryGet(agent, out var mirror) && mirror.Team == attackerTeam));
        }, new[] { AccessTools.Method(typeof(MBCommon), nameof(MBCommon.GetTotalMissionTime)) });
    }

    private static void AssertBattleEndReadsTheDefendersBeaten(IMissionAgentSpawnLogic spawnLogic, Mission mission)
    {
        var end = new BattleEndLogic();
        AccessTools.Property(typeof(MissionBehavior), nameof(MissionBehavior.Mission)).SetValue(end, mission);
        end.OnBehaviorInitialize();
        Assert.Same(spawnLogic, end._missionAgentSpawnLogic);
        end._canCheckForEndConditionSiege = true;
        end.ChangeCanCheckForEndCondition(true);
        end.CheckIsEnemySideRetreatingOrOneSideDepleted();
        Assert.True(end._isEnemySideDepleted);
        Assert.False(end._isPlayerSideDepleted);
    }
}
