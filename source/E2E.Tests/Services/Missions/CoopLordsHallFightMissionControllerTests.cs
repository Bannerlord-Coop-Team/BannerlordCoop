using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.MapEvents.TroopSupply;
using HarmonyLib;
using Missions.Battles;
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
