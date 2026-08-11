using Common;
using Common.Network;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Time;
using Moq;
using System;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace Coop.Tests.GameInterface.Services.Time;

[Collection(ModInformationRoleCollection.Name)]
public class TimeControlInterfaceTests : IDisposable
{
    private readonly bool wasServer = ModInformation.IsServer;
    private readonly Campaign previousCampaign = Campaign.Current;
    private readonly CampaignTimeControlMode previousTimeMode;

    public TimeControlInterfaceTests()
    {
        if (previousCampaign != null)
        {
            previousTimeMode = previousCampaign.TimeControlMode;
        }

        Campaign.Current = (Campaign)FormatterServices.GetUninitializedObject(typeof(Campaign));
        ModInformation.IsServer = true;
        TimePatches.OverrideTimeControlMode(CampaignTimeControlMode.StoppablePlay);
    }

    public void Dispose()
    {
        if (previousCampaign == null)
        {
            TimePatches.OverrideTimeControlMode(CampaignTimeControlMode.Stop);
            Campaign.Current = null;
        }
        else
        {
            Campaign.Current = previousCampaign;
            TimePatches.OverrideTimeControlMode(previousTimeMode);
        }

        ModInformation.IsServer = wasServer;
    }

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

    [Theory]
    [InlineData(TimeControlEnum.Play_1x)]
    [InlineData(TimeControlEnum.Play_2x)]
    public void AutomaticPause_WhenReleased_RestoresPreviousSpeed(TimeControlEnum previousSpeed)
    {
        var timeControlInterface = CreateTimeControlInterface();
        timeControlInterface.ServerSetTimeControl(previousSpeed);

        var pauseLease = timeControlInterface.ServerAcquireAutomaticPause();

        Assert.Equal(TimeControlEnum.Pause, timeControlInterface.GetTimeControl());
        Assert.True(pauseLease.TryRelease());
        Assert.Equal(previousSpeed, timeControlInterface.GetTimeControl());
    }

    [Fact]
    public void AutomaticPauses_WhenOwnersOverlap_RestoreAfterLastOwnerReleases()
    {
        var timeControlInterface = CreateTimeControlInterface();
        var firstPauseLease = timeControlInterface.ServerAcquireAutomaticPause();
        var secondPauseLease = timeControlInterface.ServerAcquireAutomaticPause();

        Assert.True(firstPauseLease.TryRelease());
        Assert.Equal(TimeControlEnum.Pause, timeControlInterface.GetTimeControl());

        Assert.True(secondPauseLease.TryRelease());
        Assert.Equal(TimeControlEnum.Play_1x, timeControlInterface.GetTimeControl());
    }

    [Fact]
    public void AutomaticPause_WhenLeaseIsReleasedTwice_DoesNotReleaseAnotherOwner()
    {
        var timeControlInterface = CreateTimeControlInterface();
        var firstPauseLease = timeControlInterface.ServerAcquireAutomaticPause();
        var secondPauseLease = timeControlInterface.ServerAcquireAutomaticPause();

        Assert.True(firstPauseLease.TryRelease());
        Assert.True(firstPauseLease.TryRelease());
        Assert.Equal(TimeControlEnum.Pause, timeControlInterface.GetTimeControl());

        Assert.True(secondPauseLease.TryRelease());
        Assert.Equal(TimeControlEnum.Play_1x, timeControlInterface.GetTimeControl());
    }

    [Fact]
    public void AutomaticPause_WhenTimeWasAlreadyPaused_DoesNotResumeTime()
    {
        var timeControlInterface = CreateTimeControlInterface();
        timeControlInterface.ServerSetTimeControl(TimeControlEnum.Pause);

        var pauseLease = timeControlInterface.ServerAcquireAutomaticPause();

        Assert.True(pauseLease.TryRelease());
        Assert.Equal(TimeControlEnum.Pause, timeControlInterface.GetTimeControl());
    }

    [Fact]
    public void AutomaticPause_WhenExplicitPauseIsRequested_DoesNotRestoreStaleSpeed()
    {
        var timeControlInterface = CreateTimeControlInterface();
        var pauseLease = timeControlInterface.ServerAcquireAutomaticPause();

        timeControlInterface.ServerSetTimeControl(TimeControlEnum.Pause);

        Assert.True(pauseLease.TryRelease());
        Assert.Equal(TimeControlEnum.Pause, timeControlInterface.GetTimeControl());
    }

    [Fact]
    public void AutomaticPause_WhenExplicitSpeedIsAccepted_DoesNotOverrideItOnRelease()
    {
        var timeControlInterface = CreateTimeControlInterface();
        var pauseLease = timeControlInterface.ServerAcquireAutomaticPause();

        timeControlInterface.ServerSetTimeControl(TimeControlEnum.Play_2x);

        Assert.Equal(TimeControlEnum.Play_2x, timeControlInterface.GetTimeControl());
        Assert.True(pauseLease.TryRelease());
        Assert.Equal(TimeControlEnum.Play_2x, timeControlInterface.GetTimeControl());
    }

    [Fact]
    public void AutomaticPause_WhenUnpausePolicyTemporarilyBlocks_RetainsOwnershipForRetry()
    {
        var timeControlInterface = CreateTimeControlInterface();
        var pauseLease = timeControlInterface.ServerAcquireAutomaticPause();
        Func<bool> policy = () => false;
        timeControlInterface.AddUnpausePolicy(policy);

        Assert.False(pauseLease.TryRelease());
        Assert.Equal(TimeControlEnum.Pause, timeControlInterface.GetTimeControl());

        timeControlInterface.RemoveUnpausePolicy(policy);

        Assert.True(pauseLease.TryRelease());
        Assert.Equal(TimeControlEnum.Play_1x, timeControlInterface.GetTimeControl());
    }

    [Fact]
    public void AutomaticPauses_WhenFinalReleaseIsBlocked_RestoreAfterPolicyAllowsIt()
    {
        var timeControlInterface = CreateTimeControlInterface();
        var firstPauseLease = timeControlInterface.ServerAcquireAutomaticPause();
        var secondPauseLease = timeControlInterface.ServerAcquireAutomaticPause();
        Func<bool> policy = () => false;
        timeControlInterface.AddUnpausePolicy(policy);

        Assert.True(firstPauseLease.TryRelease());
        Assert.False(secondPauseLease.TryRelease());
        Assert.Equal(TimeControlEnum.Pause, timeControlInterface.GetTimeControl());

        timeControlInterface.RemoveUnpausePolicy(policy);

        Assert.True(secondPauseLease.TryRelease());
        Assert.Equal(TimeControlEnum.Play_1x, timeControlInterface.GetTimeControl());
    }

    [Fact]
    public void AutomaticPause_WhenBlockedSpeedIsRequested_KeepsOriginalRestoreMode()
    {
        var timeControlInterface = CreateTimeControlInterface();
        var pauseLease = timeControlInterface.ServerAcquireAutomaticPause();
        Func<bool> policy = () => false;
        timeControlInterface.AddUnpausePolicy(policy);

        timeControlInterface.ServerSetTimeControl(TimeControlEnum.Play_2x);
        Assert.Equal(TimeControlEnum.Pause, timeControlInterface.GetTimeControl());

        timeControlInterface.RemoveUnpausePolicy(policy);

        Assert.True(pauseLease.TryRelease());
        Assert.Equal(TimeControlEnum.Play_1x, timeControlInterface.GetTimeControl());
    }

    private static TimeControlInterface CreateTimeControlInterface()
    {
        return new TimeControlInterface(
            new TimeControlModeConverter(),
            Mock.Of<INetwork>());
    }
}
