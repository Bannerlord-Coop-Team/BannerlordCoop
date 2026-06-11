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

    [Fact]
    public void CanSetTimeControl_WhenPolicyBlocksFastForward_BlocksOnlyFastForward()
    {
        // Arrange
        var timeControlInterface = CreateTimeControlInterface();
        var allowFastForward = false;
        Func<bool> policy = () => allowFastForward;

        timeControlInterface.AddFastForwardPolicy(policy);

        // Act / Assert
        Assert.True(timeControlInterface.CanSetTimeControl(TimeControlEnum.Pause));
        Assert.True(timeControlInterface.CanSetTimeControl(TimeControlEnum.Play_1x));
        Assert.False(timeControlInterface.CanSetTimeControl(TimeControlEnum.Play_2x));
    }

    [Fact]
    public void CanSetTimeControl_WhenPolicyAllowsFastForward_AllowsFastForward()
    {
        // Arrange
        var timeControlInterface = CreateTimeControlInterface();
        Func<bool> policy = () => true;

        timeControlInterface.AddFastForwardPolicy(policy);

        // Act / Assert
        Assert.True(timeControlInterface.CanSetTimeControl(TimeControlEnum.Play_2x));
    }

    [Fact]
    public void LimitTimeControl_WhenFastForwardBlocked_CapsFastForwardAtPlay1x()
    {
        // Arrange
        var timeControlInterface = CreateTimeControlInterface();
        timeControlInterface.AddFastForwardPolicy(() => false);

        // Act / Assert
        Assert.Equal(TimeControlEnum.Pause, timeControlInterface.LimitTimeControl(TimeControlEnum.Pause));
        Assert.Equal(TimeControlEnum.Play_1x, timeControlInterface.LimitTimeControl(TimeControlEnum.Play_1x));
        Assert.Equal(TimeControlEnum.Play_1x, timeControlInterface.LimitTimeControl(TimeControlEnum.Play_2x));
    }

    [Fact]
    public void LimitTimeControl_WhenUnpauseBlocked_TakesPrecedenceOverFastForwardCap()
    {
        // Arrange
        var timeControlInterface = CreateTimeControlInterface();
        timeControlInterface.AddUnpausePolicy(() => false);
        timeControlInterface.AddFastForwardPolicy(() => false);

        // Act / Assert
        Assert.Equal(TimeControlEnum.Pause, timeControlInterface.LimitTimeControl(TimeControlEnum.Play_1x));
        Assert.Equal(TimeControlEnum.Pause, timeControlInterface.LimitTimeControl(TimeControlEnum.Play_2x));
    }

    [Fact]
    public void LimitTimeControl_WhenNoPoliciesBlock_ReturnsRequestedMode()
    {
        // Arrange
        var timeControlInterface = CreateTimeControlInterface();

        // Act / Assert
        Assert.Equal(TimeControlEnum.Pause, timeControlInterface.LimitTimeControl(TimeControlEnum.Pause));
        Assert.Equal(TimeControlEnum.Play_1x, timeControlInterface.LimitTimeControl(TimeControlEnum.Play_1x));
        Assert.Equal(TimeControlEnum.Play_2x, timeControlInterface.LimitTimeControl(TimeControlEnum.Play_2x));
    }

    private static TimeControlInterface CreateTimeControlInterface()
    {
        return new TimeControlInterface(
            new TimeControlModeConverter(),
            Mock.Of<INetwork>());
    }
}
