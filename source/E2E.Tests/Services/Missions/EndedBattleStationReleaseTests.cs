using E2E.Tests.Environment.MockEngine;
using E2E.Tests.Util;
using GameInterface.Services.MapEvents;
using HarmonyLib;
using Missions.Agents.Patches;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// A co-op battle keeps its mission ticking after it ended, where vanilla's agent removal no longer releases the station, so a
/// machine went on ticking a removed user (the naval helm crash after a battle).
/// </summary>
public class EndedBattleStationReleaseTests : MissionTestEnvironment
{
    // Sets up the mission test environment with the test's output.
    public EndedBattleStationReleaseTests(ITestOutputHelper output) : base(output) { }

    // Runs vanilla's own removal, the one deaths, fades and withdrawals all reach, on an agent seated after the battle ended.
    [Fact]
    public void Removal_SeatedAgentAfterTheBattleEnded_LeavesItsStation()
    {
        using var fixture = new MissionEngineFixture();
        var client = Clients.First();
        // Installs the patch itself, right before running and after the mock-engine setup, so a copy another test removed
        // earlier doesn't matter; it's removed again in finally.
        var harmony = new Harmony("e2e.ended-battle-station-release." + Guid.NewGuid());
        harmony.CreateClassProcessor(typeof(EndedBattleStationReleasePatch)).Patch();
        BattleSpawnGate.BeginBattle("ended-battle-station-release");

        try
        {
            client.Call(() =>
            {
                var mock = fixture.CreateMission(client);
                mock.Shell._missionEnded = true;
                var agent = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.None));
                // A mock agent skips its constructor, and OnRemove walks its components last.
                AccessTools.Field(typeof(Agent), "_components").SetValue(agent, new MBList<AgentComponent>());
                agent.UseGameObject(MissionTestObjects.CreateStandingPoint());
                Assert.True(AgentMirror.TryGet(agent, out var mirror));

                agent.OnRemove();

                Assert.Null(mirror.CurrentlyUsedGameObject);
            });
        }
        finally
        {
            BattleSpawnGate.EndBattle();
            harmony.UnpatchAll(harmony.Id);
        }
    }
}
