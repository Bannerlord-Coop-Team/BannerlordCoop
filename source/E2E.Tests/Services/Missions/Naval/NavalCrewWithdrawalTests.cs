#if COOP_NAVAL
using E2E.Tests.Environment.MockEngine;
using E2E.Tests.Util;
using Missions.Battles;
using Missions.Naval;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions.Naval;

/// <summary>
/// Covers the live engine's crew withdrawal on a departed owner's hull removal, which the DLC-free naval scenarios fake.
/// </summary>
public class NavalCrewWithdrawalTests : MissionTestEnvironment
{
    public NavalCrewWithdrawalTests(ITestOutputHelper output) : base(output) { }

    // Crew off its own hull, here at another ship's helm, is not parked, and once the battle ended vanilla's removal no longer
    // frees its station, so the helm kept ticking a removed pilot and crashed in ShipControllerMachine.OnTick.
    [Fact]
    public void WithdrawAgent_CrewSeatedAtAnotherHullsHelm_LeavesTheHelmBeforeFadingOut()
    {
        using var fixture = new MissionEngineFixture();
        var client = Clients.First();

        client.Call(() =>
        {
            var mock = fixture.CreateMission(client);
            var engine = new NavalShipEngine(null!, null!, null!, null!, null!, null!, null!, null!, null!, new BattleAgentFader());
            var crew = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.None));
            crew.UseGameObject(MissionTestObjects.CreateStandingPoint());
            Assert.True(AgentMirror.TryGet(crew, out var mirror));

            engine.WithdrawAgent(crew);

            Assert.Null(mirror.CurrentlyUsedGameObject);
            Assert.False(mirror.IsActive, "the withdrawn crew member must still fade out");
        });
    }
}
#endif
