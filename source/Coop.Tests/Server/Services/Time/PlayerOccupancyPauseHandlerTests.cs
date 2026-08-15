using Common;
using Common.Network.Messages;
using Common.Util;
using Coop.Tests.Stubs;
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

    [Fact]
    public void OccupancyPause_WhenReevaluatedWhileStillOccupied_AcquiresOnce()
    {
        var timeControl = new Mock<ITimeControlInterface>();
        var pauseLease = new Mock<IAutomaticPauseLease>();
        timeControl.Setup(control => control.ServerAcquireAutomaticPause())
            .Returns(pauseLease.Object);
        var handler = CreateHandler(timeControl);

        Assert.True(handler.UpdateOccupancyTimeControl(true));
        Assert.False(handler.UpdateOccupancyTimeControl(true));

        timeControl.Verify(control => control.ServerAcquireAutomaticPause(), Times.Once);
    }

    [Fact]
    public void OccupancyPause_WhenPlayerBecomesFree_ReleasesOnce()
    {
        var timeControl = new Mock<ITimeControlInterface>();
        var pauseLease = new Mock<IAutomaticPauseLease>();
        pauseLease.Setup(lease => lease.TryRelease()).Returns(true);
        timeControl.Setup(control => control.ServerAcquireAutomaticPause())
            .Returns(pauseLease.Object);
        var handler = CreateHandler(timeControl);

        Assert.True(handler.UpdateOccupancyTimeControl(true));
        Assert.True(handler.UpdateOccupancyTimeControl(false));
        Assert.False(handler.UpdateOccupancyTimeControl(false));

        pauseLease.Verify(lease => lease.TryRelease(), Times.Once);
    }

    [Fact]
    public void OccupancyPause_WhenReleaseIsBlocked_RetriesWithoutLosingOwnership()
    {
        var timeControl = new Mock<ITimeControlInterface>();
        var pauseLease = new Mock<IAutomaticPauseLease>();
        pauseLease.SetupSequence(lease => lease.TryRelease())
            .Returns(false)
            .Returns(true);
        timeControl.Setup(control => control.ServerAcquireAutomaticPause())
            .Returns(pauseLease.Object);
        var handler = CreateHandler(timeControl);

        Assert.True(handler.UpdateOccupancyTimeControl(true));
        Assert.False(handler.UpdateOccupancyTimeControl(false));
        Assert.True(handler.UpdateOccupancyTimeControl(false));

        pauseLease.Verify(lease => lease.TryRelease(), Times.Exactly(2));
    }

    [Fact]
    public void PlayerConnectionStateChanged_WhenConnectedPlayerIsFree_ReleasesOccupancyPause()
    {
        var broker = new StubMessageBroker();
        var timeControl = new Mock<ITimeControlInterface>();
        var pauseLease = new Mock<IAutomaticPauseLease>();
        pauseLease.Setup(lease => lease.TryRelease()).Returns(true);
        timeControl.Setup(control => control.ServerAcquireAutomaticPause()).Returns(pauseLease.Object);

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
        Assert.True(handler.UpdateOccupancyTimeControl(true));

        broker.Publish(this, new PlayerConnectionStateChanged());
        DrainGameThread();

        pauseLease.Verify(lease => lease.TryRelease(), Times.Once);
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
