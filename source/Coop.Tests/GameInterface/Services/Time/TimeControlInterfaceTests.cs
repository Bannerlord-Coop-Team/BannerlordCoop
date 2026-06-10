using Common.Network;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using GameInterface.Services.Time;
using Moq;
using System;
using Xunit;

namespace Coop.Tests.GameInterface.Services.Time;

public class TimeControlInterfaceTests
{
    [Fact]
    public void CanSetTimeControl_WhenPolicyBlocksUnpause_BlocksOnlyNonPauseModes()
    {
        // Arrange
        var timeControlInterface = CreateTimeControlInterface();
        var allowUnpause = false;
        Func<bool> policy = () => allowUnpause;

        timeControlInterface.AddUnpausePolicy(policy);

        // Act / Assert
        Assert.True(timeControlInterface.CanSetTimeControl(TimeControlEnum.Pause));
        Assert.False(timeControlInterface.CanSetTimeControl(TimeControlEnum.Play_1x));
        Assert.False(timeControlInterface.CanSetTimeControl(TimeControlEnum.Play_2x));
    }

    [Fact]
    public void CanSetTimeControl_WhenAllPoliciesAllowUnpause_AllowsNonPauseModes()
    {
        // Arrange
        var timeControlInterface = CreateTimeControlInterface();
        var firstPolicyAllowsUnpause = true;
        var secondPolicyAllowsUnpause = true;
        Func<bool> firstPolicy = () => firstPolicyAllowsUnpause;
        Func<bool> secondPolicy = () => secondPolicyAllowsUnpause;

        timeControlInterface.AddUnpausePolicy(firstPolicy);
        timeControlInterface.AddUnpausePolicy(secondPolicy);

        // Act / Assert
        Assert.True(timeControlInterface.CanSetTimeControl(TimeControlEnum.Play_1x));
        Assert.True(timeControlInterface.CanSetTimeControl(TimeControlEnum.Play_2x));
    }

    private static TimeControlInterface CreateTimeControlInterface()
    {
        return new TimeControlInterface(
            new TimeControlModeConverter(),
            Mock.Of<INetwork>());
    }
}
