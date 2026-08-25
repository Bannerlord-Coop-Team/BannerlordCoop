using GameInterface.Services.Kingdoms;
using System;
using Xunit;

namespace GameInterface.Tests.Services.Kingdoms;

public class KingdomVotingRoundClockTests
{
    private static readonly DateTime RoundStartedUtc = new(2026, 8, 25, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan RoundDuration = KingdomDecisionVoteManager.VotingRoundDuration;
    private static readonly TimeSpan HoldMaximum = KingdomDecisionVoteManager.VotingRoundBattleHoldMaximum;
    private static readonly DateTime HoldLimitUtc = RoundStartedUtc + HoldMaximum;

    private static readonly Func<bool> AVoterIsInBattle = () => true;
    private static readonly Func<bool> NoVoterIsInBattle = () => false;

    private readonly KingdomVotingRoundClock clock = new();

    [Fact]
    public void CreateDeadline_EndsOneVotingRoundAfterTheRoundStarts()
    {
        Assert.Equal(TimeSpan.FromSeconds(60), RoundDuration);
        Assert.Equal(TimeSpan.FromMinutes(4), HoldMaximum);
        Assert.Equal(RoundStartedUtc + RoundDuration, clock.CreateDeadline(RoundStartedUtc));
    }

    [Fact]
    public void TryExtendDeadline_DoesNotHoldWhenNoVoterIsInBattle()
    {
        DateTime deadline = clock.CreateDeadline(RoundStartedUtc);

        Assert.Null(clock.TryExtendDeadline(deadline, deadline, RoundStartedUtc, NoVoterIsInBattle));
    }

    [Fact]
    public void TryExtendDeadline_HoldsForAnotherRoundWhileAVoterIsInBattle()
    {
        DateTime deadline = clock.CreateDeadline(RoundStartedUtc);

        DateTime? held = clock.TryExtendDeadline(deadline, deadline, RoundStartedUtc, AVoterIsInBattle);

        Assert.Equal(deadline + RoundDuration, held);
    }

    [Fact]
    public void TryExtendDeadline_ClampsTheLastHoldToTheBattleHoldMaximum()
    {
        DateTime deadline = HoldLimitUtc - TimeSpan.FromSeconds(10);

        DateTime? held = clock.TryExtendDeadline(deadline, deadline, RoundStartedUtc, AVoterIsInBattle);

        Assert.Equal(HoldLimitUtc, held);
    }

    [Fact]
    public void TryExtendDeadline_StopsHoldingOnceTheBattleHoldMaximumIsReached()
    {
        Assert.Null(clock.TryExtendDeadline(HoldLimitUtc, HoldLimitUtc, RoundStartedUtc, AVoterIsInBattle));
        Assert.Null(clock.TryExtendDeadline(
            HoldLimitUtc + TimeSpan.FromSeconds(1),
            HoldLimitUtc + TimeSpan.FromSeconds(1),
            RoundStartedUtc,
            AVoterIsInBattle));
    }

    [Fact]
    public void TryExtendDeadline_NeverHoldsPastTheBattleHoldMaximum()
    {
        DateTime deadline = clock.CreateDeadline(RoundStartedUtc);
        int holds = 0;

        while (clock.TryExtendDeadline(deadline, deadline, RoundStartedUtc, AVoterIsInBattle) is DateTime held)
        {
            Assert.True(held > deadline);
            Assert.True(held <= HoldLimitUtc);
            deadline = held;
            holds++;
        }

        Assert.Equal(HoldLimitUtc, deadline);
        Assert.Equal(3, holds);
    }

    [Fact]
    public void TryExtendDeadline_DoesNotMoveADeadlineThatIsAlreadyFurtherOut()
    {
        DateTime deadline = RoundStartedUtc + TimeSpan.FromSeconds(200);
        DateTime utcNow = RoundStartedUtc + TimeSpan.FromSeconds(100);

        Assert.Null(clock.TryExtendDeadline(utcNow, deadline, RoundStartedUtc, AVoterIsInBattle));
    }

    [Fact]
    public void TryExtendDeadline_MeasuresTheHoldFromTheLateTickRatherThanTheMissedDeadline()
    {
        DateTime deadline = clock.CreateDeadline(RoundStartedUtc);
        DateTime utcNow = deadline + TimeSpan.FromSeconds(30);

        DateTime? held = clock.TryExtendDeadline(utcNow, deadline, RoundStartedUtc, AVoterIsInBattle);

        Assert.Equal(utcNow + RoundDuration, held);
    }

    [Fact]
    public void TryExtendDeadline_ClampsALateTickThatIsStillInsideTheBattleHoldMaximum()
    {
        DateTime deadline = HoldLimitUtc - TimeSpan.FromSeconds(60);
        DateTime utcNow = HoldLimitUtc - TimeSpan.FromSeconds(40);

        DateTime? held = clock.TryExtendDeadline(utcNow, deadline, RoundStartedUtc, AVoterIsInBattle);

        Assert.Equal(HoldLimitUtc, held);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(60)]
    public void TryExtendDeadline_NeverReturnsADeadlineThatHasAlreadyPassed(int secondsPastTheHoldMaximum)
    {
        DateTime deadline = HoldLimitUtc - TimeSpan.FromSeconds(1);
        DateTime utcNow = HoldLimitUtc + TimeSpan.FromSeconds(secondsPastTheHoldMaximum);

        Assert.Null(clock.TryExtendDeadline(utcNow, deadline, RoundStartedUtc, AVoterIsInBattle));
    }

    [Fact]
    public void TryExtendDeadline_LooksForBattlesWhileAHoldIsStillPossible()
    {
        DateTime deadline = clock.CreateDeadline(RoundStartedUtc);
        int battleScans = 0;

        clock.TryExtendDeadline(deadline, deadline, RoundStartedUtc, () =>
        {
            battleScans++;
            return true;
        });

        Assert.Equal(1, battleScans);
    }

    [Fact]
    public void TryExtendDeadline_DoesNotLookForBattlesOncePastTheBattleHoldMaximum()
    {
        int battleScans = 0;

        DateTime? held = clock.TryExtendDeadline(HoldLimitUtc, HoldLimitUtc, RoundStartedUtc, () =>
        {
            battleScans++;
            return true;
        });

        Assert.Null(held);
        Assert.Equal(0, battleScans);
    }
}
