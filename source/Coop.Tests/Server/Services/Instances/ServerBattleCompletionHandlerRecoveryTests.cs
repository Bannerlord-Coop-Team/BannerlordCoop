using Common;
using Common.Tests.Utils;
using Common.Util;
using Coop.Core.Server.Services.Instances;
using Coop.Core.Server.Services.Instances.Handlers;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.Players;
using LiteNetLib;
using Moq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using Xunit;

namespace Coop.Tests.Server.Services.Instances;

/// <summary>
/// Covers what the coordinator does once a battle conclusion claim has timed out: retry the battle while
/// its map event is still running, and clean up for good once it is not.
/// </summary>
public class ServerBattleCompletionHandlerRecoveryTests : IDisposable
{
    private const string MapEventId = "map-event-1";
    private const string HostControllerId = "host";

    private static readonly ConstructorInfo PeerConstructor = typeof(NetPeer).GetConstructor(
        BindingFlags.NonPublic | BindingFlags.Instance,
        binder: null,
        new[] { typeof(NetManager), typeof(IPEndPoint), typeof(int) },
        modifiers: null)!;

    private readonly TestMessageBroker broker = new();
    private readonly Mock<IObjectManager> objectManager = new();
    private readonly Mock<IPlayerManager> playerManager = new();
    private readonly Mock<IBattleHostRegistry> hostRegistry = new();
    private readonly Mock<IBattleCompletionTracker> completionTracker = new();
    private readonly MissionManager missionManager;
    private readonly ServerBattleCompletionHandler handler;
    private readonly NetPeer peer;

    public ServerBattleCompletionHandlerRecoveryTests()
    {
        peer = (NetPeer)PeerConstructor.Invoke(new object[]
        {
            new NetManager(null),
            new IPEndPoint(IPAddress.Loopback, 52100),
            1,
        });

        var mappedPeer = peer;
        playerManager
            .Setup(manager => manager.TryGetPeer(HostControllerId, out mappedPeer))
            .Returns(true);

        // A claim that is due the moment it is made, so no test has to wait for the deadline.
        missionManager = new MissionManager(playerManager.Object) { ConclusionDeadline = TimeSpan.Zero };

        var assignment = new BattleHostAssignment(HostControllerId, Array.Empty<string>(), epoch: 1);
        hostRegistry
            .Setup(registry => registry.TryGet(MapEventId, out assignment))
            .Returns(true);

        completionTracker
            .Setup(tracker => tracker.TryReconcile(
                MapEventId,
                It.IsAny<IReadOnlyCollection<string>>(),
                HostControllerId,
                1,
                out It.Ref<BattleState>.IsAny,
                It.IsAny<bool>()))
            .Returns((
                string _,
                IReadOnlyCollection<string> _,
                string _,
                int _,
                out BattleState concludedState,
                bool _) =>
            {
                concludedState = BattleState.DefenderVictory;
                return true;
            });

        handler = new ServerBattleCompletionHandler(
            broker,
            missionManager,
            objectManager.Object,
            playerManager.Object,
            hostRegistry.Object,
            completionTracker.Object);
    }

    public void Dispose() => handler.Dispose();

    // The handler marshals its work onto the game thread, which this assembly's pump runs. A blocking
    // no-op queued behind that work returns once the work itself has run.
    private static void Drain() => GameThread.Run(() => { }, blocking: true);

    [Fact]
    public void ResultArrivingAfterItsClaimTimedOutIsRetriedInsteadOfDropped()
    {
        GiveTheMapEventAnActiveState();
        EnterMissionAndClaimTheConclusion();

        broker.Publish(this, new BattleStateChangeProcessed(MapEventId, BattleState.DefenderVictory, applied: false));
        Drain();
        broker.Publish(this, new CampaignTick());
        Drain();

        Assert.Single(broker.Messages.GetMessages<AuthoritativeBattleConclusionRequested>());
    }

    [Fact]
    public void ClaimThatTimesOutWithoutAnyResultIsRecoveredByTheNextTick()
    {
        GiveTheMapEventAnActiveState();
        EnterMissionAndClaimTheConclusion();

        broker.Publish(this, new CampaignTick());
        Drain();

        Assert.Single(broker.Messages.GetMessages<AuthoritativeBattleConclusionRequested>());
    }

    [Fact]
    public void TimedOutClaimForAMapEventThatIsGoneIsCleanedUpInsteadOfRetried()
    {
        MapEvent missing = null;
        objectManager
            .Setup(manager => manager.TryGetObject(MapEventId, out missing))
            .Returns(false);
        EnterMissionAndClaimTheConclusion();

        broker.Publish(this, new CampaignTick());
        Drain();

        completionTracker.Verify(tracker => tracker.Clear(MapEventId), Times.Once);
        Assert.Empty(broker.Messages.GetMessages<AuthoritativeBattleConclusionRequested>());
    }

    private void GiveTheMapEventAnActiveState()
    {
        // A map event built without its constructor reads as running: not finalized, no victory yet.
        MapEvent mapEvent = ObjectHelper.SkipConstructor<MapEvent>();
        objectManager
            .Setup(manager => manager.TryGetObject(MapEventId, out mapEvent))
            .Returns(true);
    }

    private void EnterMissionAndClaimTheConclusion()
    {
        Assert.True(missionManager.TryEnterMission(peer, HostControllerId, MapEventId, out _));
        Assert.True(missionManager.TryBeginActiveInstanceConclusion(
            MapEventId, new[] { HostControllerId }));
    }
}
