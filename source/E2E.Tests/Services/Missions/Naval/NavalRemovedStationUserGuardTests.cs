#if COOP_NAVAL
using Autofac;
using E2E.Tests.Environment.MockEngine;
using E2E.Tests.Util;
using HarmonyLib;
using Missions.Naval;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions.Naval;

/// <summary>
/// Covers the guard that frees a hull machine's seat a removed agent still holds, which the mock mission can build agents for
/// while Coop.Tests cannot.
/// </summary>
public class NavalRemovedStationUserGuardTests : MissionTestEnvironment
{
    public NavalRemovedStationUserGuardTests(ITestOutputHelper output) : base(output, configureDependencies: builder =>
        builder.RegisterInstance(new ActiveNavalBattle()).As<INavalMissionTracker>()) { }

    // Once the battle ended vanilla's removal leaves a helm pilot seated, and the helm's tick then read the freed agent. The guard
    // frees that seat on both sides before the tick, and leaves a live agent's seat alone.
    [Fact]
    public void Prefix_HelmSeatingARemovedAgent_FreesOnlyThatSeat()
    {
        using var fixture = new MissionEngineFixture();
        var client = Clients.First();
        var prefix = AccessTools.Method("Missions.Naval.NavalRemovedStationUserPatch:Prefix");

        client.Call(() =>
        {
            var mock = fixture.CreateMission(client);
            var removed = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.None));
            var live = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.None));
            var pilotPoint = Seat(removed);
            var otherPoint = Seat(live);
            removed._isRemoved = true;
            // NavalDLC is only a runtime dependency of this project.
            var helm = (UsableMachine)MissionTestObjects.Uninitialized(
                AccessTools.TypeByName("NavalDLC.Missions.Objects.UsableMachines.ShipControllerMachine"));
            helm.StandingPoints = new MBList<StandingPoint> { pilotPoint, otherPoint };

            prefix.Invoke(null, new object[] { helm });

            Assert.Null(pilotPoint.UserAgent);
            Assert.Null(removed.CurrentlyUsedGameObject);
            Assert.Same(live, otherPoint.UserAgent);
            Assert.Same(otherPoint, live.CurrentlyUsedGameObject);
        });
    }

    // A standing point the agent uses, linked both ways as vanilla's OnUse leaves it.
    private static StandingPoint Seat(Agent agent)
    {
        var point = MissionTestObjects.CreateStandingPoint();
        AccessTools.Field(typeof(UsableMissionObject), "_components").SetValue(point, new List<UsableMissionObjectComponent>());
        point._userAgent = agent;
        agent.UseGameObject(point);
        return point;
    }

    /// <summary>A naval battle that is always running on the client, so the guard acts.</summary>
    private sealed class ActiveNavalBattle : INavalMissionTracker
    {
        public bool IsActive { get; set; } = true;
    }
}
#endif
