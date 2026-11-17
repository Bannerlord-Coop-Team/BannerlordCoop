using E2E.Tests.Environment.MockEngine;
using E2E.Tests.Services.Missions;
using GameInterface.Services.MapEvents;
using GameInterface.Services.Sessions;
using Missions.Sessions;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Sessions;

public class SessionMissionHostTests : MissionTestEnvironment
{
    public SessionMissionHostTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void CreateMissionBehaviors_BeginsSpawnGate_ReturnsController()
    {
        using var fixture = new MissionEngineFixture();
        var client = Clients.First();
        SetControllerId(client, "owner");
        var sessionId = SessionInstanceId.Create();

        client.Call(() =>
        {
            CreateConnectedMission(fixture, client, sessionId);
            BattleSpawnGate.EndBattle();

            var behaviors = client.Resolve<ISessionMissionHost>().CreateMissionBehaviors(sessionId);

            var controller = Assert.IsType<CoopSessionController>(Assert.Single(behaviors));
            Assert.Equal(sessionId, BattleSpawnGate.ActiveMapEventId);
            controller.OnBehaviorInitialize();
            Assert.Equal(sessionId, controller.Session.InstanceId);
            BattleSpawnGate.EndBattle();
        });
    }

    [Fact]
    public void CreateMissionBehaviors_RejectsNonSessionId()
    {
        using var fixture = new MissionEngineFixture();
        var client = Clients.First();

        client.Call(() =>
        {
            CreateConnectedMission(fixture, client, "12|34");
            BattleSpawnGate.EndBattle();
            var host = client.Resolve<ISessionMissionHost>();

            Assert.Empty(host.CreateMissionBehaviors("12|34"));
            Assert.Empty(host.CreateMissionBehaviors(null));
            Assert.False(BattleSpawnGate.IsCoopBattleActive);
        });
    }

    [Fact]
    public void IsLocalHost_FollowsRegistry()
    {
        var client = Clients.First();
        SetControllerId(client, "owner");
        var sessionId = SessionInstanceId.Create();

        client.Call(() =>
        {
            var host = client.Resolve<ISessionMissionHost>();
            var registry = client.Resolve<IBattleHostRegistry>();
            BattleSpawnGate.BeginBattle(sessionId);
            try
            {
                Assert.False(host.IsLocalHost);

                registry.Set(sessionId, new BattleHostAssignment("other", System.Array.Empty<string>(), 1));
                Assert.False(host.IsLocalHost);

                registry.Set(sessionId, new BattleHostAssignment("owner", System.Array.Empty<string>(), 2));
                Assert.True(host.IsLocalHost);
            }
            finally
            {
                BattleSpawnGate.EndBattle();
            }
            Assert.False(host.IsLocalHost);
        });
    }
}
