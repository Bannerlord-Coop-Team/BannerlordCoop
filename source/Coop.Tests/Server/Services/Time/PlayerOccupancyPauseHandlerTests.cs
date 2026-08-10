using Common;
using Common.Network.Messages;
using Common.Util;
using Coop.Tests.Stubs;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using Moq;
using System;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace Coop.Tests.Server.Services.Time;

[Collection(ModInformationRoleCollection.Name)]
public class PlayerOccupancyPauseHandlerTests : IDisposable
{
    private readonly bool wasServer = ModInformation.IsServer;

    public PlayerOccupancyPauseHandlerTests()
    {
        ModInformation.IsServer = true;
    }

    public void Dispose()
    {
        ModInformation.IsServer = wasServer;
    }

    [Theory]
    [InlineData(TimeControlEnum.Play_1x)]
    [InlineData(TimeControlEnum.Play_2x)]
    public void OccupancyPause_WhenPlayerBecomesFree_RestoresPreviousSpeed(TimeControlEnum previousSpeed)
    {
        var timeControl = new Mock<ITimeControlInterface>();
        var pauseLeaseMock = new Mock<IAutomaticPauseLease>();
        pauseLeaseMock.SetupGet(lease => lease.AppliedPause).Returns(true);
        timeControl.Setup(control => control.ServerAcquireAutomaticPause(null))
            .Returns(pauseLeaseMock.Object);
        TimeControlEnum? restoredSpeed = previousSpeed;
        pauseLeaseMock.Setup(lease => lease.TryRelease(out restoredSpeed))
            .Returns(true);
        var handler = CreateHandler(timeControl);

        Assert.Equal(TimeControlEnum.Pause, handler.UpdateOccupancyTimeControl(true));
        Assert.Equal(previousSpeed, handler.UpdateOccupancyTimeControl(false));

        timeControl.Verify(control => control.ServerAcquireAutomaticPause(null), Times.Once);
        pauseLeaseMock.Verify(lease => lease.TryRelease(out restoredSpeed), Times.Once);
    }

    [Fact]
    public void OccupancyPause_WhenTimeWasAlreadyPaused_DoesNotResume()
    {
        var timeControl = new Mock<ITimeControlInterface>();
        var pauseLeaseMock = new Mock<IAutomaticPauseLease>();
        pauseLeaseMock.SetupGet(lease => lease.AppliedPause).Returns(false);
        TimeControlEnum? restoredSpeed = null;
        pauseLeaseMock.Setup(lease => lease.TryRelease(out restoredSpeed))
            .Returns(true);
        timeControl.Setup(control => control.ServerAcquireAutomaticPause(null))
            .Returns(pauseLeaseMock.Object);
        var handler = CreateHandler(timeControl);

        Assert.Null(handler.UpdateOccupancyTimeControl(true));
        Assert.Null(handler.UpdateOccupancyTimeControl(false));

        timeControl.Verify(control => control.ServerAcquireAutomaticPause(null), Times.Once);
        pauseLeaseMock.Verify(lease => lease.TryRelease(out restoredSpeed), Times.Once);
    }

    [Fact]
    public void OccupancyPause_WhenTimeChangedWhileOccupied_DoesNotOverrideNewSpeed()
    {
        var timeControl = new Mock<ITimeControlInterface>();
        var pauseLeaseMock = new Mock<IAutomaticPauseLease>();
        pauseLeaseMock.SetupGet(lease => lease.AppliedPause).Returns(true);
        timeControl.Setup(control => control.ServerAcquireAutomaticPause(null))
            .Returns(pauseLeaseMock.Object);
        TimeControlEnum? restoredSpeed = null;
        pauseLeaseMock.Setup(lease => lease.TryRelease(out restoredSpeed))
            .Returns(true);
        var handler = CreateHandler(timeControl);

        Assert.Equal(TimeControlEnum.Pause, handler.UpdateOccupancyTimeControl(true));
        Assert.Null(handler.UpdateOccupancyTimeControl(false));

        pauseLeaseMock.Verify(lease => lease.TryRelease(out restoredSpeed), Times.Once);
    }

    [Fact]
    public void OccupancyPause_WhenAnotherAutomaticPauseRemains_ReleasesItsOwnership()
    {
        var timeControl = new Mock<ITimeControlInterface>();
        var pauseLeaseMock = new Mock<IAutomaticPauseLease>();
        pauseLeaseMock.SetupGet(lease => lease.AppliedPause).Returns(true);
        timeControl.Setup(control => control.ServerAcquireAutomaticPause(null))
            .Returns(pauseLeaseMock.Object);
        TimeControlEnum? restoredSpeed = null;
        pauseLeaseMock.Setup(lease => lease.TryRelease(out restoredSpeed))
            .Returns(true);
        var handler = CreateHandler(timeControl);

        Assert.Equal(TimeControlEnum.Pause, handler.UpdateOccupancyTimeControl(true));
        Assert.Null(handler.UpdateOccupancyTimeControl(false));
        Assert.Null(handler.UpdateOccupancyTimeControl(false));

        pauseLeaseMock.Verify(lease => lease.TryRelease(out restoredSpeed), Times.Once);
    }

    [Fact]
    public void OccupancyPause_WhenRestoreIsBlocked_RetriesWithoutLosingOwnership()
    {
        var timeControl = new Mock<ITimeControlInterface>();
        var pauseLeaseMock = new Mock<IAutomaticPauseLease>();
        pauseLeaseMock.SetupGet(lease => lease.AppliedPause).Returns(true);
        timeControl.Setup(control => control.ServerAcquireAutomaticPause(null))
            .Returns(pauseLeaseMock.Object);
        TimeControlEnum? restoredSpeed = TimeControlEnum.Play_1x;
        pauseLeaseMock.SetupSequence(lease => lease.TryRelease(out restoredSpeed))
            .Returns(false)
            .Returns(true);
        var handler = CreateHandler(timeControl);

        Assert.Equal(TimeControlEnum.Pause, handler.UpdateOccupancyTimeControl(true));
        Assert.Null(handler.UpdateOccupancyTimeControl(false));
        Assert.Equal(TimeControlEnum.Play_1x, handler.UpdateOccupancyTimeControl(false));
        pauseLeaseMock.Verify(lease => lease.TryRelease(out restoredSpeed), Times.Exactly(2));
    }

    [Fact]
    public void OccupancyPause_WhenPriorLeaseIsInactive_AcquiresFreshOwnership()
    {
        var timeControl = new Mock<ITimeControlInterface>();
        var staleLease = new Mock<IAutomaticPauseLease>();
        staleLease.SetupGet(lease => lease.AppliedPause).Returns(true);
        staleLease.SetupGet(lease => lease.IsActive).Returns(false);
        var freshLease = new Mock<IAutomaticPauseLease>();
        freshLease.SetupGet(lease => lease.AppliedPause).Returns(true);
        timeControl.SetupSequence(control => control.ServerAcquireAutomaticPause(null))
            .Returns(staleLease.Object)
            .Returns(freshLease.Object);
        var handler = CreateHandler(timeControl);

        Assert.Equal(TimeControlEnum.Pause, handler.UpdateOccupancyTimeControl(true));
        Assert.Equal(TimeControlEnum.Pause, handler.UpdateOccupancyTimeControl(true));

        timeControl.Verify(control => control.ServerAcquireAutomaticPause(null), Times.Exactly(2));
    }

    [Fact]
    public void PlayerConnectionStateChanged_WhenConnectedPlayerIsFree_ReleasesOccupancyPause()
    {
        var broker = new StubMessageBroker();
        var timeControl = new Mock<ITimeControlInterface>();
        var pauseLease = new Mock<IAutomaticPauseLease>();
        pauseLease.SetupGet(lease => lease.AppliedPause).Returns(true);
        TimeControlEnum? restoredSpeed = TimeControlEnum.Play_1x;
        pauseLease.Setup(lease => lease.TryRelease(out restoredSpeed)).Returns(true);
        timeControl.Setup(control => control.ServerAcquireAutomaticPause(null)).Returns(pauseLease.Object);

        var player = new Player("controller", string.Empty, "party", string.Empty, string.Empty);
        var playerManager = new Mock<IPlayerManager>();
        playerManager.SetupGet(manager => manager.Players).Returns(new[] { player });
        playerManager.Setup(manager => manager.IsConnected(player)).Returns(true);
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        party.Party = ObjectHelper.SkipConstructor<PartyBase>();
        party.Party.MobileParty = party;
        var objectManager = new Mock<IObjectManager>();
        objectManager.Setup(manager => manager.TryGetObject<MobileParty>("party", out party)).Returns(true);
        using var handler = new PlayerOccupancyPauseHandler(
            broker,
            objectManager.Object,
            playerManager.Object,
            timeControl.Object);
        Assert.Equal(TimeControlEnum.Pause, handler.UpdateOccupancyTimeControl(true));

        broker.Publish(this, new PlayerConnectionStateChanged());
        DrainGameThread();

        pauseLease.Verify(lease => lease.TryRelease(out restoredSpeed), Times.Once);
    }

    private static void DrainGameThread() => GameThread.Run(() => { }, blocking: true);

    private static PlayerOccupancyPauseHandler CreateHandler(Mock<ITimeControlInterface> timeControl)
    {
        return new PlayerOccupancyPauseHandler(
            new StubMessageBroker(),
            Mock.Of<IObjectManager>(),
            Mock.Of<IPlayerManager>(),
            timeControl.Object);
    }
}
