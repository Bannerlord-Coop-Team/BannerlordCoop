using Common;
using Common.Messaging;
using E2E.Tests.Environment.MockEngine;
using HarmonyLib;
using Missions.Battles;
using Missions.Messages;
using Moq;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// The walls end into the keep: the host's end logic once its defenders fell back and kept running, and every other
/// walls client on the host's accepted pull-back.
/// </summary>
public class CoopSiegeWallsEndLogicTests : MissionTestEnvironment
{
    private const float ReinforcementInterval = 10f;

    // Vanilla's end logic builds its timers from the native mission clock, which a headless mission doesn't have.
    private static readonly MethodBase[] FixedMissionClock =
        { AccessTools.Method(typeof(MBCommon), nameof(MBCommon.GetTotalMissionTime)) };

    public CoopSiegeWallsEndLogicTests(ITestOutputHelper output) : base(output) { }

    // Vanilla counts the routed defenders into the pull-back; the walls then end once the rest kept running for
    // vanilla's retreat wait (reinforcement interval + 3 s).
    [Fact]
    public void HostDefendersFallBackAndKeepRunning_EndTheWallsPushedBack()
    {
        using var fixture = new MissionEngineFixture();
        var client = Clients.First();
        client.Call(() =>
        {
            var mock = fixture.CreateMission(client);
            var attackerTeam = mock.AddTeam(BattleSideEnum.Attacker);
            mock.PlayerTeam = mock.AttackerTeam;
            var defenderTeam = mock.AddTeam(BattleSideEnum.Defender);
            var spawnLogic = new Mock<IMissionAgentSpawnLogic>();
            spawnLogic.Setup(logic => logic.GetReinforcementInterval(BattleSideEnum.Defender)).Returns(ReinforcementInterval);
            var end = new CoopSiegeWallsEndLogic(defenderTroopsForPullBack: 1);
            AccessTools.Property(typeof(MissionBehavior), nameof(MissionBehavior.Mission)).SetValue(end, mock.Shell);
            end.OnBehaviorInitialize();
            end._missionAgentSpawnLogic = spawnLogic.Object;
            end.ChangeCanCheckForEndCondition(true);

            mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop).Team(attackerTeam).Controller(AgentControllerType.Player));
            var routed = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop).Team(defenderTeam).Controller(AgentControllerType.AI));
            var holdout = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop).Team(defenderTeam).Controller(AgentControllerType.AI));
            Assert.True(AgentMirror.TryGet(routed, out var routedMirror));

            mock.CurrentTime = 1f;
            routedMirror.IsActive = false;
            end.OnAgentRemoved(routed, null, AgentState.Routed, default);
            Tick(end);
            Assert.True(end._isEnemyDefenderPulledBack);

            // The last defender standing at 1 s turns to run; the walls hold until the retreat wait passes.
            AccessTools.Property(typeof(Agent), nameof(Agent.IsRunningAway)).SetValue(holdout, true);
            MissionResult result = null;
            mock.CurrentTime = 1f + ReinforcementInterval;
            Tick(end);
            Assert.False(end.MissionEnded(ref result));

            mock.CurrentTime = 1f + ReinforcementInterval + 3.5f;
            Tick(end);
            Assert.True(end.MissionEnded(ref result));
            Assert.Equal(BattleState.DefenderPullBack, result.BattleState);
            Assert.False(result.PlayerVictory);
            Assert.False(result.PlayerDefeated);
        }, FixedMissionClock);
    }

    // A walls client whose own defenders are still standing ends on the host's pull-back once its deployment commits.
    [Fact]
    public void AcceptedPullBack_EndsAWallsClientWhoseDefendersStillStand()
    {
        using var fixture = new MissionEngineFixture();
        var (mapEventId, _) = SetupCoopBattle("host", "joiner");
        var host = Clients.First();
        var joiner = Clients.Skip(1).First();

        host.Call(() => CreateConnectedMission(fixture, host, mapEventId));
        EnterBattle(host, mapEventId);

        joiner.Call(() =>
        {
            var mock = CreateConnectedMission(fixture, joiner, mapEventId);
            mock.PlayerTeam = mock.AttackerTeam;
            var missionProperty = AccessTools.Property(typeof(MissionBehavior), nameof(MissionBehavior.Mission));
            var end = new CoopSiegeWallsEndLogic(defenderTroopsForPullBack: 20);
            missionProperty.SetValue(end, mock.Shell);
            end._missionAgentSpawnLogic = new Mock<IMissionAgentSpawnLogic>().Object;
            end.ChangeCanCheckForEndCondition(true);
            mock.Shell.MissionBehaviors.Add(end);
            var defenderTeam = mock.AddTeam(BattleSideEnum.Defender);
            mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop).Team(defenderTeam).Controller(AgentControllerType.AI));

            var controller = joiner.Resolve<CoopBattleController>();
            missionProperty.SetValue(controller, mock.Shell);
            controller.Session.TryBegin(mapEventId);
            joiner.Resolve<IMessageBroker>().Publish(host, new NetworkBattleResultSnapshot(
                mapEventId,
                "host",
                controller.Session.HostEpoch,
                BattleState.DefenderPullBack));
            GameThread.Run(() => { }, blocking: true);

            MissionResult result = null;
            Assert.False(end.MissionEnded(ref result));

            controller.OnDeploymentFinished();

            Assert.True(end.MissionEnded(ref result));
            Assert.Equal(BattleState.DefenderPullBack, result.BattleState);
            Assert.False(result.PlayerVictory);
        }, FixedMissionClock);
    }

    // Restarting vanilla's retreat-check timer keeps its own depletion read, which needs a live mission, out of the tick.
    private static void Tick(CoopSiegeWallsEndLogic end)
    {
        end._checkRetreatingTimer = new BasicMissionTimer();
        end.OnMissionTick(0.1f);
    }

}
