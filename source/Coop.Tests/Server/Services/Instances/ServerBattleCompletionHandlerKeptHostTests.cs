using Common;
using Common.Tests.Utils;
using Coop.Core.Server.Services.Instances;
using Coop.Core.Server.Services.Instances.Handlers;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.Players;
using Missions.Messages;
using Moq;
using System;
using TaleWorlds.Core;
using Xunit;

namespace Coop.Tests.Server.Services.Instances;

/// <summary>Q7: a kept naval host's battle stops waiting for its remaining members once the deadline after it left passed.</summary>
public class ServerBattleCompletionHandlerKeptHostTests : IDisposable
{
    private const string MapEventId = "map-event-kept";

    private readonly TestMessageBroker broker = new();
    private readonly Mock<IBattleHostRegistry> hostRegistry = new();
    private readonly Mock<IBattleCompletionTracker> completionTracker = new();
    private readonly Mock<IMissionManager> missionManager = new();
    private readonly ServerBattleCompletionHandler handler;

    public ServerBattleCompletionHandlerKeptHostTests()
    {
        missionManager.Setup(manager => manager.TakeExpiredConclusions()).Returns(Array.Empty<string>());
        handler = new ServerBattleCompletionHandler(
            broker,
            missionManager.Object,
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

    // S21 M1: the first member of a new instance on the same map event drops the deadline of the instance that emptied, so
    // it never stops the new battle from waiting for its members.
    [Fact]
    public void FirstMemberOfANewInstance_DropsTheOldKeptHostDeadline()
    {
        SetAssignment(kept: true);
        handler.KeptHostReportTimeout = TimeSpan.Zero;
        Publish(new BattleHostAssignmentChanged(MapEventId));

        Publish(new MissionMemberEntered("newcomer", MapEventId, isFirstMember: true));
        Publish(new CampaignTick());

        completionTracker.Verify(tracker => tracker.StopWaitingForMembers(It.IsAny<string>()), Times.Never);
    }

    // A later member of the same instance keeps the deadline.
    [Fact]
    public void LaterMemberOfTheSameInstance_KeepsTheKeptHostDeadline()
    {
        SetAssignment(kept: true);
        handler.KeptHostReportTimeout = TimeSpan.Zero;
        Publish(new BattleHostAssignmentChanged(MapEventId));

        Publish(new MissionMemberEntered("rejoiner", MapEventId, isFirstMember: false));
        Publish(new CampaignTick());

        completionTracker.Verify(tracker => tracker.StopWaitingForMembers(MapEventId), Times.Once);
    }

    // An applied conclusion of the battle drops its deadline.
    [Fact]
    public void AppliedConclusion_DropsTheKeptHostDeadline()
    {
        SetAssignment(kept: true);
        handler.KeptHostReportTimeout = TimeSpan.Zero;
        missionManager.Setup(manager => manager.CompleteInstanceConclusion(MapEventId, true)).Returns(true);
        Publish(new BattleHostAssignmentChanged(MapEventId));

        Publish(new BattleStateChangeProcessed(MapEventId, BattleState.DefenderVictory, applied: true));
        Publish(new CampaignTick());

        completionTracker.Verify(tracker => tracker.StopWaitingForMembers(It.IsAny<string>()), Times.Never);
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
