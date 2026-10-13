#if DEBUG
using Missions.Naval;
using System;
using System.Collections.Generic;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class NavalScenarioReadyPressTests
{
    private readonly List<Action> queue = new List<Action>();
    private readonly NavalScenarioReadyPress readyPress;
    private bool canPress = true;
    private int pressed;

    public NavalScenarioReadyPressTests()
    {
        readyPress = new NavalScenarioReadyPress(queue.Add);
    }

    [Fact]
    public void Tick_QueuesThePressInsteadOfPressingInsideTheMissionTick()
    {
        Tick(0.016f);

        Assert.Equal(0, pressed);
        Assert.Equal(0, readyPress.Presses);
        Assert.Single(queue);

        RunQueued();

        Assert.Equal(1, pressed);
        Assert.Equal(1, readyPress.Presses);
    }

    [Fact]
    public void Tick_DoesNotQueueWhileReadyWouldRefuse()
    {
        canPress = false;

        Tick(10f);

        Assert.Empty(queue);
    }

    [Fact]
    public void Tick_QueuesOnlyOnePressUntilTheQueuedOneRuns()
    {
        Tick(0.016f);
        Tick(NavalScenarioReadyPress.RetrySeconds + 1f);

        Assert.Single(queue);
    }

    [Fact]
    public void QueuedPress_SkipsWhenReadyStoppedAcceptingBeforeItRan()
    {
        Tick(0.016f);
        canPress = false;

        RunQueued();

        Assert.Equal(0, pressed);
        Assert.Equal(0, readyPress.Presses);
    }

    [Fact]
    public void Tick_RetriesAfterTheCooldownWhenThePressDidNotFinishDeployment()
    {
        Tick(0.016f);
        RunQueued();

        Tick(NavalScenarioReadyPress.RetrySeconds / 2f);
        Assert.Empty(queue);

        Tick(NavalScenarioReadyPress.RetrySeconds);
        RunQueued();

        Assert.Equal(2, pressed);
        Assert.Equal(2, readyPress.Presses);
    }

    private void Tick(float dt) => readyPress.Tick(dt, () => canPress, () => pressed++);

    private void RunQueued()
    {
        var actions = queue.ToArray();
        queue.Clear();
        foreach (var action in actions) action();
    }
}
#endif
