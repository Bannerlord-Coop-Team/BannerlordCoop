#if DEBUG
using GameInterface.Services.MapEvents.NavalScenarios;
using System.Linq;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents.NavalScenarios;

public class NavalScenarioStateTests
{
    private static NavalScenarioConfig Config(string preset, params string[] players)
    {
        Assert.True(new NavalScenarioResolver().TryResolve(new[] { preset }.Concat(players).ToArray(), players,
            out var config, out var error), error);
        return config;
    }

    private static string[] Steps(NavalScenarioConfig config, string localControllerId, bool isServer = false) =>
        NavalScenarioState.StepsFor(config, localControllerId, isServer).ToArray();

    [Fact]
    public void StepsFor_TheAttackedPlayerEntersTheBattleAndEveryPlayerPressesReady()
    {
        var config = Config("vs_pirates", "PlayerOne", "PlayerTwo");

        Assert.Equal(new[] { NavalScenarioState.AttackStep, NavalScenarioState.ReadyStep }, Steps(config, "PlayerOne"));
        Assert.Equal(new[] { NavalScenarioState.ReadyStep }, Steps(config, "PlayerTwo"));
    }

    [Fact]
    public void StepsFor_PlacementPresetsAddTheirTimelineSteps()
    {
        Assert.Equal(new[] { NavalScenarioState.ReadyStep, NavalScenarioState.PlaceStep, NavalScenarioState.RopeStep },
            Steps(Config("boarding", "PlayerOne", "PlayerTwo"), "PlayerTwo"));
        Assert.Equal(new[] { NavalScenarioState.ReadyStep, NavalScenarioState.PlaceStep, NavalScenarioState.HelmStep },
            Steps(Config("ramming", "PlayerOne", "PlayerTwo"), "PlayerTwo"));
        Assert.Equal(new[] { NavalScenarioState.ReadyStep, NavalScenarioState.DisconnectStep, NavalScenarioState.RejoinStep },
            Steps(Config("migration", "PlayerOne", "PlayerTwo"), "PlayerTwo"));
    }

    // M8b: the migration preset disconnects the battle host, or with who=nonhost the first player that is not the host.
    [Fact]
    public void DisconnectingControllerId_IsTheHost_OrTheFirstOtherPlayerForWhoNonHost()
    {
        var host = Config("migration", "PlayerOne", "PlayerTwo", "PlayerThree");
        var nonHost = new NavalScenarioResolver().TryResolve(
            new[] { "migration", "who=nonhost", "PlayerOne", "PlayerTwo", "PlayerThree" },
            new[] { "PlayerOne", "PlayerTwo", "PlayerThree" }, out var config, out var error) ? config : null;
        Assert.NotNull(nonHost);

        Assert.Equal("PlayerTwo", host.DisconnectingControllerId("PlayerTwo"));
        Assert.Equal("PlayerTwo", nonHost!.DisconnectingControllerId("PlayerOne"));
        Assert.Equal("PlayerOne", nonHost.DisconnectingControllerId("PlayerTwo"));
        Assert.Null(nonHost.DisconnectingControllerId(null));
        Assert.Null(Config("vs_pirates", "PlayerOne", "PlayerTwo").DisconnectingControllerId("PlayerOne"));
    }

    // Every client carries the step; only the battle host hits the pre-damaged AI hull.
    [Fact]
    public void StepsFor_TheSinkingPresetHitsThePreDamagedHullAfterDeploying()
    {
        var config = Config("sinking", "PlayerOne", "PlayerTwo");

        Assert.Equal(new[] { NavalScenarioState.AttackStep, NavalScenarioState.ReadyStep, NavalScenarioState.SinkStep },
            Steps(config, "PlayerOne"));
        Assert.Equal(new[] { NavalScenarioState.ReadyStep, NavalScenarioState.SinkStep }, Steps(config, "PlayerTwo"));
        Assert.DoesNotContain(NavalScenarioState.SinkStep, Steps(Config("vs_pirates", "PlayerOne"), "PlayerOne"));
    }

    // M8a: only the second player retreats; the others stay in the battle to watch its hulls leave.
    [Fact]
    public void StepsFor_TheRetreatPresetRetreatsOnlyTheSecondPlayer()
    {
        var config = Config("retreat", "PlayerOne", "PlayerTwo", "PlayerThree");

        Assert.Equal(new[] { NavalScenarioState.AttackStep, NavalScenarioState.ReadyStep }, Steps(config, "PlayerOne"));
        Assert.Equal(new[] { NavalScenarioState.ReadyStep, NavalScenarioState.RetreatStep }, Steps(config, "PlayerTwo"));
        Assert.Equal(new[] { NavalScenarioState.ReadyStep }, Steps(config, "PlayerThree"));
        Assert.Empty(Steps(config, null, isServer: true));
    }

    [Fact]
    public void StepsFor_TheServerJoinsTheLateJoinerWhoThenEntersOnItsOwn()
    {
        var config = Config("late_join", "PlayerOne", "PlayerTwo");

        Assert.Equal(new[] { NavalScenarioState.LateJoinStep }, Steps(config, null, isServer: true));
        Assert.Empty(Steps(Config("vs_pirates", "PlayerOne"), null, isServer: true));
        Assert.Equal(new[] { NavalScenarioState.AttackStep, NavalScenarioState.ReadyStep }, Steps(config, "PlayerOne"));
        Assert.Equal(new[] { NavalScenarioState.LateJoinAttackStep, NavalScenarioState.ReadyStep }, Steps(config, "PlayerTwo"));
    }

    [Fact]
    public void StepsFor_AClientOutsideTheScenarioRunsNothing()
    {
        var config = Config("boarding", "PlayerOne");

        Assert.Empty(Steps(config, "PlayerTwo"));
        Assert.Empty(Steps(config, null));
        Assert.Empty(Steps(null, "PlayerOne"));
    }

    [Fact]
    public void Start_ReplacesTheScenarioAndPhasesOnlyMoveForward()
    {
        var state = new NavalScenarioState();
        Assert.Equal(NavalScenarioPhase.None, state.Phase);
        state.Advance(NavalScenarioPhase.Fighting);
        Assert.Equal(NavalScenarioPhase.None, state.Phase);

        var first = Config("vs_pirates", "PlayerOne");
        state.Start(first, "PlayerOne", isServer: false);
        Assert.Same(first, state.Current);
        Assert.Equal(NavalScenarioPhase.Staging, state.Phase);

        state.Advance(NavalScenarioPhase.Fighting);
        state.Advance(NavalScenarioPhase.Deploying);
        Assert.Equal(NavalScenarioPhase.Fighting, state.Phase);

        var second = Config("solo", "PlayerOne");
        state.Start(second, "PlayerOne", isServer: false);
        Assert.Same(second, state.Current);
        Assert.Equal(NavalScenarioPhase.Staging, state.Phase);
        Assert.All(state.Steps, step => Assert.Equal(NavalScenarioStepStatus.Pending, step.Status));
    }

    [Fact]
    public void Complete_RecordsTheFirstOutcomeOfAPendingStepOnly()
    {
        var state = new NavalScenarioState();
        state.Start(Config("vs_pirates", "PlayerOne"), "PlayerOne", isServer: false);

        Assert.True(state.IsPending(NavalScenarioState.AttackStep));
        state.Complete(NavalScenarioState.AttackStep, false, "refused");
        state.Complete(NavalScenarioState.AttackStep, true, "accepted");
        state.Complete(NavalScenarioState.RopeStep, true, "not a step of this scenario");

        var attack = state.Steps.Single(step => step.Name == NavalScenarioState.AttackStep);
        Assert.False(state.IsPending(NavalScenarioState.AttackStep));
        Assert.Equal(NavalScenarioStepStatus.Failed, attack.Status);
        Assert.Equal("refused", attack.Detail);
        Assert.DoesNotContain(state.Steps, step => step.Name == NavalScenarioState.RopeStep);
    }
}
#endif
