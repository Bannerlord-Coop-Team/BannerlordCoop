using System.Linq;
using E2E.Tests.Environment;
using E2E.Tests.Environment.MockEngine;
using Missions.Battles;
using Missions.Messages;
using TaleWorlds.Core;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// Verifies that the battle controller reports resolved native mission results to the campaign server and ignores
/// mission exits that did not resolve the battle.
/// </summary>
public class CoopBattleResultCommitTests : MissionTestEnvironment
{
    public CoopBattleResultCommitTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void ResolvedDefeat_ReportsResultToCampaignServer()
    {
        using var fixture = new MissionEngineFixture();
        var host = Clients.First();
        SetControllerId(host, "host");

        host.Call(() =>
        {
            var mock = fixture.CreateMission(host);
            mock.Shell.MissionResult = new MissionResult(BattleState.DefenderVictory, playerVictory: false, playerDefeated: true, enemyRetreated: false);

            var controller = host.Resolve<CoopBattleController>();
            controller.Session.TryBegin("mapEvent1");
            host.NetworkSentMessages.Clear();
            controller.ResultCommitter.ReportResolvedResult(mock.Shell.MissionResult);

            GC.KeepAlive(controller);
        });

        var report = Assert.Single(host.NetworkSentMessages.GetMessages<NetworkBattleResultReady>());
        Assert.Equal("mapEvent1", report.InstanceId);
        Assert.Equal(BattleState.DefenderVictory, report.BattleState);
        Assert.Equal(0, report.HostEpoch);
    }

    [Fact]
    public void UnresolvedMissionExit_DoesNotReportResult()
    {
        using var fixture = new MissionEngineFixture();
        var host = Clients.First();
        SetControllerId(host, "host");

        host.Call(() =>
        {
            var mock = fixture.CreateMission(host);
            mock.Shell.MissionResult = new MissionResult();

            var controller = host.Resolve<CoopBattleController>();
            controller.Session.TryBegin("mapEvent1");
            host.NetworkSentMessages.Clear();
            controller.ResultCommitter.ReportResolvedResult(mock.Shell.MissionResult);

            GC.KeepAlive(controller);
        });

        Assert.Empty(host.NetworkSentMessages.GetMessages<NetworkBattleResultReady>());
    }
}
