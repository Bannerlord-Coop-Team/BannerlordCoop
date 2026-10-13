using E2E.Tests.Environment.MockEngine;
using Missions.Battles;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

public class AgentFormationAssignerTests : MissionTestEnvironment
{
    public AgentFormationAssignerTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void Assign_PutsTheAgentInTheFormationSlot()
    {
        using var fixture = new MissionEngineFixture();
        var client = Clients.First();
        client.Call(() =>
        {
            var mission = fixture.CreateMission(client);
            var agent = SpawnDefender(mission);

            var formation = new AgentFormationAssigner().Assign(agent, (int)FormationClass.Ranged);

            Assert.Same(mission.DefenderTeam.GetFormation(FormationClass.Ranged).Shell, formation);
            Assert.Same(formation, agent.Formation);
        });
    }

    // A naval formation without a hull refuses agents (NavalAgentFormationAssigner); they keep the formation they had.
    [Fact]
    public void Assign_ToAFormationThatCannotTakeAgents_LeavesTheAgentWhereItWas()
    {
        using var fixture = new MissionEngineFixture();
        var client = Clients.First();
        client.Call(() =>
        {
            var mission = fixture.CreateMission(client);
            var agent = SpawnDefender(mission);
            var assigner = new RefusingFormationAssigner();

            Assert.Null(assigner.Assign(agent, (int)FormationClass.Ranged));
            Assert.Null(agent.Formation);

            var infantry = mission.DefenderTeam.GetFormation(FormationClass.Infantry).Shell;
            agent.Formation = infantry;
            Assert.Null(assigner.Assign(agent));
            Assert.Same(infantry, agent.Formation);
        });
    }

    private static Agent SpawnDefender(MockMission mission) =>
        mission.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop)
            .Controller(AgentControllerType.None).Team(mission.DefenderTeam.Shell));

    private sealed class RefusingFormationAssigner : AgentFormationAssigner
    {
        protected override bool CanJoin(Formation formation) => false;
    }
}
