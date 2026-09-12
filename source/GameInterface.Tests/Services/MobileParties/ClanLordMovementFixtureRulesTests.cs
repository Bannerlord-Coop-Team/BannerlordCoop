#if DEBUG
using GameInterface.Services.MobileParties.Commands;
using System;
using System.Collections.Generic;
using Xunit;

namespace GameInterface.Tests.Services.MobileParties;

public class ClanLordMovementFixtureRulesTests
{
    private readonly IClanLordMovementFixtureRules rules = new ClanLordMovementFixtureRules();

    [Theory]
    [InlineData("lord_1_63_1", 0)]
    [InlineData("lord_1_74", 1)]
    [InlineData("lord_1_74_1", 2)]
    [InlineData("lord_1_1", 3)]
    [InlineData("LORD_1_63_1", 3)]
    [InlineData(null, 3)]
    public void LordPriority_PrefersExactFixtureLordIds(string? heroId, int expected)
    {
        Assert.Equal(expected, rules.LordPriority(heroId));
    }

    [Theory]
    [InlineData(0.1f, 0f, 5f, 0f)]
    [InlineData(0f, 0.1f, 0f, 5f)]
    [InlineData(0.08f, 0.08f, 5f, 5f)]
    [InlineData(0.5f, 0f, 1f, 0f)]
    public void ValidateMovement_AcceptsMovementWithRemainingDistance(
        float currentX, float currentY, float targetX, float targetY)
    {
        Assert.Null(rules.ValidateMovement(10, 11, 0, 0, currentX, currentY,
            targetX, targetY, targetX, targetY, true, false, false));
    }

    [Theory]
    [InlineData(10)]
    [InlineData(9)]
    public void ValidateMovement_RejectsSamplesWithoutTimeAdvancing(double nowTicks)
    {
        string failure = rules.ValidateMovement(10, nowTicks, 0, 0, 1, 0,
            5, 0, 5, 0, true, false, false);

        Assert.Contains("time has not advanced", failure);
    }

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(0.099f, 0f)]
    [InlineData(0f, 0.099f)]
    [InlineData(0.06f, 0.06f)]
    public void ValidateMovement_RejectsInsufficientDisplacement(float currentX, float currentY)
    {
        string failure = rules.ValidateMovement(10, 11, 0, 0, currentX, currentY,
            5, 0, 5, 0, true, false, false);

        Assert.Contains("moved less than 0.1", failure);
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(0.501f)]
    public void ValidateMovement_RejectsSamplesTooCloseToDestination(float currentX)
    {
        string failure = rules.ValidateMovement(10, 11, 0, 0, currentX, 0,
            1, 0, 1, 0, true, false, false);

        Assert.Contains("less than 0.5", failure);
    }

    [Theory]
    [InlineData(0.01f, 0f, true)]
    [InlineData(0f, 0.01f, true)]
    [InlineData(0.011f, 0f, false)]
    [InlineData(0f, 0.011f, false)]
    [InlineData(0.008f, 0.008f, false)]
    public void ValidateMovement_ChecksTargetErrorAsDistance(float actualX, float actualY, bool accepted)
    {
        string failure = rules.ValidateMovement(10, 11, -5, 0, -4, 0,
            0, 0, actualX, actualY, true, false, false);

        if (accepted)
            Assert.Null(failure);
        else
            Assert.Contains("target differs", failure);
    }

    [Theory]
    [InlineData(false, false, false, "behavior does not match")]
    [InlineData(true, true, false, "still waiting")]
    [InlineData(true, false, true, "AI is still disabled")]
    public void ValidateMovement_RejectsUnrecoveredState(
        bool coherentBehavior, bool waiting, bool aiDisabled, string reason)
    {
        string failure = rules.ValidateMovement(10, 11, 0, 0, 1, 0,
            5, 0, 5, 0, coherentBehavior, waiting, aiDisabled);

        Assert.Contains(reason, failure);
    }

    [Theory]
    [MemberData(nameof(NonFiniteInputs))]
    public void ValidateMovement_RejectsEveryNonFiniteInput(int index, double invalidValue)
    {
        double[] values = { 10, 11, 0, 0, 1, 0, 5, 0, 5, 0 };
        values[index] = invalidValue;

        string failure = rules.ValidateMovement(values[0], values[1],
            (float)values[2], (float)values[3], (float)values[4], (float)values[5],
            (float)values[6], (float)values[7], (float)values[8], (float)values[9],
            true, false, false);

        Assert.Contains("non-finite", failure);
    }

    public static IEnumerable<object[]> NonFiniteInputs()
    {
        for (int index = 0; index < 10; index++)
        {
            yield return new object[] { index, double.NaN };
            yield return new object[] { index, double.PositiveInfinity };
            yield return new object[] { index, double.NegativeInfinity };
        }
    }
}
#endif
