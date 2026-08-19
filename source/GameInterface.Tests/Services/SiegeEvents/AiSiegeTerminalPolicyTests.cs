using GameInterface.Services.SiegeEvents;
using Moq;
using Serilog;
using Xunit;

namespace GameInterface.Tests.Services.SiegeEvents;

public class AiSiegeTerminalPolicyTests
{
    private static AiSiegeTerminalPolicy CreatePolicy()
    {
        return new AiSiegeTerminalPolicy(
            new Mock<IAiSiegeAssaultReadiness>().Object,
            new Mock<ILogger>().Object);
    }

    [Fact]
    public void StarvingPreparedViableAiSiege_Assaults()
    {
        var decision = CreatePolicy().GetDecision(CreateContext(isAssaultViable: true));

        Assert.Equal(AiSiegeTerminalDecision.Assault, decision);
    }

    [Fact]
    public void StarvingPreparedInviableAiSiege_Withdraws()
    {
        var decision = CreatePolicy().GetDecision(CreateContext(isAssaultViable: false));

        Assert.Equal(AiSiegeTerminalDecision.Withdraw, decision);
    }

    [Fact]
    public void ActiveTransition_DoesNotStartDuplicateTerminalAction()
    {
        var decision = CreatePolicy().GetDecision(CreateContext(
            isAssaultViable: true,
            hasActiveTransition: true));

        Assert.Equal(AiSiegeTerminalDecision.None, decision);
    }

    [Fact]
    public void PlayerLedSiege_IsUnaffected()
    {
        var decision = CreatePolicy().GetDecision(CreateContext(
            isAssaultViable: true,
            isPlayerLed: true));

        Assert.Equal(AiSiegeTerminalDecision.None, decision);
    }

    [Fact]
    public void EndedSiegeState_IsCleanAcrossPolicyInstances()
    {
        var context = CreateContext(isAssaultViable: true, isCurrentSiege: false);

        Assert.Equal(AiSiegeTerminalDecision.None, CreatePolicy().GetDecision(context));
        Assert.Equal(AiSiegeTerminalDecision.None, CreatePolicy().GetDecision(context));
    }

    [Fact]
    public void StarvingUnpreparedSiege_Withdraws()
    {
        var decision = CreatePolicy().GetDecision(CreateContext(
            isAssaultViable: true,
            isPrepared: false));

        Assert.Equal(AiSiegeTerminalDecision.Withdraw, decision);
    }

    private static AiSiegeTerminalContext CreateContext(
        bool isAssaultViable,
        bool isPrepared = true,
        bool isPlayerLed = false,
        bool isCurrentSiege = true,
        bool hasActiveTransition = false)
    {
        return new AiSiegeTerminalContext(
            isFoodProblem: true,
            isPrepared,
            isPlayerLed,
            isCurrentSiege,
            hasActiveTransition,
            isAssaultViable);
    }
}
