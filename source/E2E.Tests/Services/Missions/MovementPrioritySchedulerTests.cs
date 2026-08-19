using Missions.Agents.Handlers;
using System;
using Xunit;

namespace E2E.Tests.Services.Missions;

public sealed class MovementPrioritySchedulerTests
{
    private readonly MovementPriorityScheduler scheduler = new MovementPriorityScheduler();

    [Fact]
    public void EqualAge_CloserAgentHasPriority()
    {
        MovementPriorityKey close = Create(distance: 5f, currentTime: 10f, lastSentTime: 9.9f);
        MovementPriorityKey far = Create(distance: 75f, currentTime: 10f, lastSentTime: 9.9f);

        Assert.True(scheduler.Compare(close, far) < 0);
    }

    [Fact]
    public void EqualDistance_OlderAgentHasPriority()
    {
        MovementPriorityKey older = Create(distance: 25f, currentTime: 10f, lastSentTime: 9.7f);
        MovementPriorityKey newer = Create(distance: 25f, currentTime: 10f, lastSentTime: 9.95f);

        Assert.True(scheduler.Compare(older, newer) < 0);
    }

    [Fact]
    public void FarthestAgentCrossesFreshNearestAgentAfterThreeHalfLives()
    {
        MovementPriorityKey freshNear = Create(distance: 0f, currentTime: 10f, lastSentTime: 10f);
        MovementPriorityKey staleFar = Create(
            distance: MovementPriorityScheduler.InterestRadius,
            currentTime: 10f,
            lastSentTime: 10f - MovementPriorityScheduler.MaximumPriorityAgingSeconds - 0.001f);

        Assert.True(scheduler.Compare(staleFar, freshNear) < 0);
    }

    [Fact]
    public void ZeroDistanceAgentDoesNotRemainPermanentlyFirst()
    {
        MovementPriorityKey justSentAtZero = Create(distance: 0f, currentTime: 5f, lastSentTime: 5f);
        MovementPriorityKey staleFar = Create(distance: 75f, currentTime: 5f, lastSentTime: 4.7f);

        Assert.True(scheduler.Compare(staleFar, justSentAtZero) < 0);
    }

    [Fact]
    public void MissionTimeOffsetDoesNotChangeOrdering()
    {
        MovementPriorityKey first = Create(distance: 20f, currentTime: 2f, lastSentTime: 1.9f);
        MovementPriorityKey second = Create(distance: 50f, currentTime: 2f, lastSentTime: 1.7f);
        MovementPriorityKey shiftedFirst = Create(distance: 20f, currentTime: 1002f, lastSentTime: 1001.9f);
        MovementPriorityKey shiftedSecond = Create(distance: 50f, currentTime: 1002f, lastSentTime: 1001.7f);

        Assert.Equal(
            Math.Sign(scheduler.Compare(first, second)),
            Math.Sign(scheduler.Compare(shiftedFirst, shiftedSecond)));
    }

    [Fact]
    public void MissingFocusUsesAgeOnlyOrdering()
    {
        MovementPriorityKey older = Create(distance: null, currentTime: 10f, lastSentTime: 9.7f);
        MovementPriorityKey newer = Create(distance: null, currentTime: 10f, lastSentTime: 9.9f);

        Assert.True(scheduler.Compare(older, newer) < 0);
    }

    [Fact]
    public void LocalMainAgentWinsBeforeScoreComparison()
    {
        MovementPriorityKey main = Create(
            distance: 75f,
            currentTime: 10f,
            lastSentTime: 10f,
            isMain: true);
        MovementPriorityKey staleNearbyAgent = Create(
            distance: 0f,
            currentTime: 10f,
            lastSentTime: 9f);

        Assert.True(scheduler.Compare(main, staleNearbyAgent) < 0);
    }

    private MovementPriorityKey Create(
        float? distance,
        float currentTime,
        float? lastSentTime,
        bool isMain = false)
    {
        return scheduler.CreateKey(
            isMain,
            distance,
            currentTime,
            lastSentTime,
            pendingSince: lastSentTime ?? currentTime,
            Guid.NewGuid());
    }
}
