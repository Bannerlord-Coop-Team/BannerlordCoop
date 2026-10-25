using Common;
using Common.Tests.Utils;
using Coop.Core.Server.Services.Instances;
using Coop.Core.Server.Services.Instances.Handlers;
using GameInterface.Services.MapEvents;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.Players;
using Missions.Messages;
using Moq;
using System;
using Xunit;

namespace Coop.Tests.Server.Services.Instances;

/// <summary>Q7: a kept naval host's battle stops waiting for its remaining members once the deadline after it left passed.</summary>
public class ServerBattleCompletionHandlerKeptHostTests : IDisposable
{
    private const string MapEventId = "map-event-kept";

    private readonly TestMessageBroker broker = new();
    private readonly Mock<IBattleHostRegistry> hostRegistry = new();
    private readonly Mock<IBattleCompletionTracker> completionTracker = new();
    private readonly ServerBattleCompletionHandler handler;

    public ServerBattleCompletionHandlerKeptHostTests()
    {
        handler = new ServerBattleCompletionHandler(
            broker,
            new MissionManager(Mock.Of<IPlayerManager>()),
            Mock.Of<IObjectManager>(),
            Mock.Of<IPlayerManager>(),
            hostRegistry.Object,
            completionTracker.Object);
    }

    public void Dispose() => handler.Dispose();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void KeptAssignment_StopsWaitingForMembersOnceTheDeadlinePassed(bool kept)
    {
        SetAssignment(kept);
        handler.KeptHostReportTimeout = TimeSpan.Zero;

        Publish(new BattleHostAssignmentChanged(MapEventId));
        Publish(new CampaignTick());
        Publish(new CampaignTick());

        completionTracker.Verify(tracker => tracker.StopWaitingForMembers(MapEventId), kept ? Times.Once() : Times.Never());
    }

    [Fact]
    public void KeptAssignment_WaitsUntilTheDeadline()
    {
        SetAssignment(kept: true);

        Publish(new BattleHostAssignmentChanged(MapEventId));
        Publish(new CampaignTick());

        completionTracker.Verify(tracker => tracker.StopWaitingForMembers(It.IsAny<string>()), Times.Never);
        Assert.Equal(TimeSpan.FromMinutes(2), handler.KeptHostReportTimeout);
    }

    // The server's assignment for the battle, kept after its host left or not.
    private void SetAssignment(bool kept)
    {
        var assignment = new BattleHostAssignment("host", Array.Empty<string>(), epoch: 1, isNaval: true, keptAfterResult: kept);
        hostRegistry.Setup(registry => registry.TryGet(MapEventId, out assignment)).Returns(true);
    }

    // Publishes on the server and waits for the handler's serialized game-thread work.
    private void Publish<T>(T message) where T : Common.Messaging.IEvent
    {
        broker.Publish(this, message);
        GameThread.Run(() => { }, blocking: true);
    }
}
