#if DEBUG
using Common;
using Common.Commands;
using Common.Network;
using E2E.Tests.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents.NavalScenarios;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

/// <summary>
/// coop.debug.naval.scenario's server staging and the scenario message the clients act on. The naval mission steps need
/// the game and are left to live runs.
/// </summary>
public class NavalScenarioTests : SyncTestBase
{
    public NavalScenarioTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void ScenarioMessage_ReachesEveryClientAndOnlyAListedPlayerGetsSteps()
    {
        var clients = Clients.ToArray();
        string listedId = ControllerIdOf(clients[0]);
        Assert.True(new NavalScenarioResolver().TryResolve(new[] { "boarding", "initiator-elsewhere", listedId },
            new[] { "initiator-elsewhere", listedId }, out var config, out var error), error);
        config.MapEventId = "MapEvent_scenario";

        Server.Call(() => Server.Resolve<INetwork>().SendAll(new NetworkNavalScenarioStarted(config)));
        foreach (var client in clients) client.PumpGameThread();

        var listed = clients[0].Resolve<INavalScenarioState>();
        Assert.NotNull(listed.Current);
        Assert.Equal(config.ScenarioId, listed.Current!.ScenarioId);
        Assert.Equal("MapEvent_scenario", listed.Current.MapEventId);
        Assert.Equal(new[] { "initiator-elsewhere", listedId }, listed.Current.ControllerIds);
        Assert.Equal(NavalScenarioPlacement.Boarding, listed.Current.Placement);
        Assert.Equal(10f, listed.Current.Spacing);
        Assert.Equal(NavalScenarioPhase.Staging, listed.Phase);
        Assert.Equal(new[] { NavalScenarioState.ReadyStep, NavalScenarioState.PlaceStep, NavalScenarioState.RopeStep },
            listed.Steps.Select(step => step.Name));
        Assert.All(listed.Steps, step => Assert.Equal(NavalScenarioStepStatus.Pending, step.Status));

        var outsider = clients[1].Resolve<INavalScenarioState>();
        Assert.Equal(config.ScenarioId, outsider.Current?.ScenarioId);
        Assert.Empty(outsider.Steps);

        Assert.Null(Server.Resolve<INavalScenarioState>().Current);
    }

    [Fact]
    public void ScenarioCommand_RefusesAPlayerWhoIsNotConnected()
    {
        var result = RunOnServer(() => Server.Resolve<ICoopCommandRegistry>().ProcessCommand("coop.debug.naval.scenario",
            new CoopCommandArgsFactory().FromValues(new[] { "vs_pirates", "hulls=2", "ghost-player" })));

        Assert.False(result.Succeeded);
        Assert.Equal("Player ghost-player is not connected; coop.debug.players.list shows the current ids.", result.Output);
        AssertNoClientScenario();
    }

    [Fact]
    public void Stager_RefusesAnUnregisteredPlayerBeforeTouchingTheCampaign()
    {
        Assert.True(new NavalScenarioResolver().TryResolve(new[] { "vs_pirates", "ghost-player" }, new[] { "ghost-player" },
            out var config, out var error), error);

        bool staged = true;
        string? output = null;
        RunOnServer(() =>
        {
            staged = Server.Resolve<INavalScenarioStager>().TryStage(config, out output);
            return null;
        });

        Assert.False(staged);
        Assert.Equal("No registered player has controller id ghost-player.", output);
        Assert.Equal("", config.MapEventId);
        Assert.Null(Server.Resolve<INavalScenarioState>().Current);
        AssertNoClientScenario();
    }

    private CoopCommandResult RunOnServer(Func<CoopCommandResult?> action)
    {
        CoopCommandResult? result = null;
        bool originalNavalDlcActive = ModInformation.IsNavalDlcActive;
        try
        {
            ModInformation.IsNavalDlcActive = true;
            Server.Call(() => result = action());
        }
        finally
        {
            ModInformation.IsNavalDlcActive = originalNavalDlcActive;
        }

        return result!;
    }

    private void AssertNoClientScenario()
    {
        foreach (var client in Clients)
        {
            client.PumpGameThread();
            Assert.Null(client.Resolve<INavalScenarioState>().Current);
        }
    }

    private static string ControllerIdOf(Environment.Instance.EnvironmentInstance client)
    {
        var provider = client.Resolve<IControllerIdProvider>();
        if (string.IsNullOrEmpty(provider.ControllerId)) provider.SetControllerId("scenario-client");
        return provider.ControllerId;
    }
}
#endif
