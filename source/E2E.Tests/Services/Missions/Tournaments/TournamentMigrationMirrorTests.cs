using System.Reflection;
using E2E.Tests.Environment.MockEngine;
using Missions.Tournaments;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions.Tournaments;

public class TournamentMigrationMirrorTests : MissionTestEnvironment
{
    public TournamentMigrationMirrorTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void HostMigration_ConvertsTransferredRiderAndMountToAi()
    {
        using var fixture = new MissionEngineFixture();
        var newHost = Clients.First();
        SetControllerId(newHost, "new-host");

        newHost.Call(() =>
        {
            var mock = fixture.CreateMission(newHost);
            var controller = newHost.Resolve<CoopTournamentController>();
            Assert.True(controller.Session.TryApplyState(
                "session", "mission", 1, 1, "match", "new-host", Array.Empty<string>()));

            Agent rider = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.None));
            Agent mount = mock.SpawnMount(rider);

            MethodInfo wakeTransferredAgent = typeof(CoopTournamentController).GetMethod(
                "WakeTransferredAgent",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(wakeTransferredAgent);
            wakeTransferredAgent.Invoke(controller, new object[] { rider });

            Assert.True(AgentMirror.TryGet(rider, out var riderMirror));
            Assert.True(AgentMirror.TryGet(mount, out var mountMirror));
            Assert.Equal(AgentControllerType.AI, riderMirror.Controller);
            Assert.Equal(AgentControllerType.AI, mountMirror.Controller);
        });
    }
}
