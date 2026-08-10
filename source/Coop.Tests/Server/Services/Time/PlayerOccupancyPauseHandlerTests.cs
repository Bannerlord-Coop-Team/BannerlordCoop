using Coop.Tests.Stubs;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Moq;
using Xunit;

namespace Coop.Tests.Server.Services.Time;

public class PlayerOccupancyPauseHandlerTests
{
    [Theory]
    [InlineData(TimeControlEnum.Play_1x)]
    [InlineData(TimeControlEnum.Play_2x)]
    public void OccupancyPause_WhenPlayerBecomesFree_RestoresPreviousSpeed(TimeControlEnum previousSpeed)
    {
        var timeControl = new Mock<ITimeControlInterface>();
        long pauseRevision = 7;
        timeControl.Setup(control => control.ServerTryCreatePause(out previousSpeed, out pauseRevision))
            .Returns(true);
        var restoredSpeed = previousSpeed;
        timeControl.Setup(control => control.ServerTryRestoreTimeControl(
                pauseRevision,
                out restoredSpeed))
            .Returns(AutomaticPauseRestoreResult.Restored);
        var handler = CreateHandler(timeControl);

        Assert.Equal(TimeControlEnum.Pause, handler.UpdateOccupancyTimeControl(true));
        Assert.Equal(previousSpeed, handler.UpdateOccupancyTimeControl(false));

        timeControl.Verify(control => control.ServerTryCreatePause(out previousSpeed, out pauseRevision), Times.Once);
        timeControl.Verify(control => control.ServerTryRestoreTimeControl(
            pauseRevision,
            out restoredSpeed), Times.Once);
    }

    [Fact]
    public void OccupancyPause_WhenTimeWasAlreadyPaused_DoesNotResume()
    {
        var timeControl = new Mock<ITimeControlInterface>();
        TimeControlEnum previousSpeed = default;
        long pauseRevision = default;
        timeControl.Setup(control => control.ServerTryCreatePause(out previousSpeed, out pauseRevision))
            .Returns(false);
        var handler = CreateHandler(timeControl);

        Assert.Null(handler.UpdateOccupancyTimeControl(true));
        Assert.Null(handler.UpdateOccupancyTimeControl(false));

        timeControl.Verify(
            control => control.ServerTryRestoreTimeControl(
                It.IsAny<long>(),
                out It.Ref<TimeControlEnum>.IsAny),
            Times.Never);
    }

    [Fact]
    public void OccupancyPause_WhenTimeChangedWhileOccupied_DoesNotOverrideNewSpeed()
    {
        var timeControl = new Mock<ITimeControlInterface>();
        var previousSpeed = TimeControlEnum.Play_1x;
        long pauseRevision = 11;
        timeControl.Setup(control => control.ServerTryCreatePause(out previousSpeed, out pauseRevision))
            .Returns(true);
        TimeControlEnum restoredSpeed = default;
        timeControl.Setup(control => control.ServerTryRestoreTimeControl(
                pauseRevision,
                out restoredSpeed))
            .Returns(AutomaticPauseRestoreResult.Stale);
        var handler = CreateHandler(timeControl);

        Assert.Equal(TimeControlEnum.Pause, handler.UpdateOccupancyTimeControl(true));
        Assert.Null(handler.UpdateOccupancyTimeControl(false));

        timeControl.Verify(control => control.ServerTryRestoreTimeControl(
            pauseRevision,
            out restoredSpeed), Times.Once);
    }

    [Fact]
    public void OccupancyPause_WhenAnotherAutomaticPauseRemains_ReleasesItsOwnership()
    {
        var timeControl = new Mock<ITimeControlInterface>();
        var previousSpeed = TimeControlEnum.Play_1x;
        long pauseToken = 12;
        timeControl.Setup(control => control.ServerTryCreatePause(out previousSpeed, out pauseToken))
            .Returns(true);
        TimeControlEnum restoredSpeed = default;
        timeControl.Setup(control => control.ServerTryRestoreTimeControl(
                pauseToken,
                out restoredSpeed))
            .Returns(AutomaticPauseRestoreResult.StillPaused);
        var handler = CreateHandler(timeControl);

        Assert.Equal(TimeControlEnum.Pause, handler.UpdateOccupancyTimeControl(true));
        Assert.Null(handler.UpdateOccupancyTimeControl(false));
        Assert.Null(handler.UpdateOccupancyTimeControl(false));

        timeControl.Verify(control => control.ServerTryRestoreTimeControl(
            pauseToken,
            out restoredSpeed), Times.Once);
    }

    [Fact]
    public void OccupancyPause_WhenRestoreIsBlocked_RetriesWithoutLosingOwnership()
    {
        var timeControl = new Mock<ITimeControlInterface>();
        var previousSpeed = TimeControlEnum.Play_1x;
        long pauseRevision = 3;
        timeControl.Setup(control => control.ServerTryCreatePause(out previousSpeed, out pauseRevision))
            .Returns(true);
        var restoredSpeed = previousSpeed;
        timeControl.SetupSequence(control => control.ServerTryRestoreTimeControl(
                pauseRevision,
                out restoredSpeed))
            .Returns(AutomaticPauseRestoreResult.Blocked)
            .Returns(AutomaticPauseRestoreResult.Restored);
        var handler = CreateHandler(timeControl);

        Assert.Equal(TimeControlEnum.Pause, handler.UpdateOccupancyTimeControl(true));
        Assert.Null(handler.UpdateOccupancyTimeControl(false));
        Assert.Equal(previousSpeed, handler.UpdateOccupancyTimeControl(false));
        timeControl.Verify(control => control.ServerTryRestoreTimeControl(
            pauseRevision,
            out restoredSpeed), Times.Exactly(2));
    }

    private static PlayerOccupancyPauseHandler CreateHandler(Mock<ITimeControlInterface> timeControl)
    {
        return new PlayerOccupancyPauseHandler(
            new StubMessageBroker(),
            Mock.Of<IObjectManager>(),
            Mock.Of<IPlayerManager>(),
            timeControl.Object);
    }
}
