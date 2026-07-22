using GameInterface.Services.MapEvents;
using Xunit;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// Pure capacity math for the BR-110 engine agent budget. The spawn-path behavior (reinforcement parties,
/// puppets, supplier waves honoring the budget) lives in <see cref="BattleAgentRenderCapTests"/>.
/// </summary>
public class BattleAgentBudgetTests
{
    private readonly IBattleAgentBudget budget = new BattleAgentBudget();

    [Fact]
    [Trait("Requirement", "BR-110")]
    public void EngineLimit_IsTwoThousandAgents()
    {
        Assert.Equal(2000, budget.MaxRenderedAgents);
    }

    [Theory]
    [Trait("Requirement", "BR-110")]
    [InlineData(0, 2000)]
    [InlineData(1, 1999)]
    [InlineData(1999, 1)]
    [InlineData(2000, 0)]
    [InlineData(2500, 0)] // already past the limit — never negative
    public void RemainingCapacity_IsLimitMinusLiveAndNeverNegative(int liveAgents, int expected)
    {
        Assert.Equal(expected, budget.RemainingCapacity(liveAgents));
    }

    [Fact]
    [Trait("Requirement", "BR-110")]
    public void NullMission_HasCapacityAndDoesNotClamp()
    {
        // No mission means no engine to overload — suppliers driven outside a mission stay unclamped.
        Assert.True(budget.HasCapacityFor(null, 5000));
        Assert.Equal(300, budget.ClampToCapacity(null, 300));
        Assert.Equal(0, budget.CountLiveAgents(null));
    }
}
