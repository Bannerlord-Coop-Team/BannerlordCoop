using Common.Messaging;
using GameInterface.Services.GameDebug.Messages;
using Missions.Battles;
#if DEBUG
using Missions.Diagnostics;
#endif

namespace E2E.Tests.Services.Missions;

public class BattleDebugRouteHandlerTests
{
#if DEBUG
    [Fact]
    public void TakeSnapshotTail_KeepsMostRecentBoundedEvents()
    {
        int[] timeline = Enumerable.Range(
            0,
            MissionActionDiagnostics.MaximumSnapshotTimelineEvents + 2).ToArray();

        int[] snapshot = MissionActionDiagnostics.TakeSnapshotTail(timeline);

        Assert.Equal(MissionActionDiagnostics.MaximumSnapshotTimelineEvents, snapshot.Length);
        Assert.Equal(2, snapshot[0]);
        Assert.Equal(timeline[^1], snapshot[^1]);
    }

#endif

    [Fact]
    public void RouteMessage_KeepsWeakSubscriptionAlive()
    {
        using var messageBroker = new InspectableMessageBroker();
        using var handler = new BattleDebugRouteHandler(messageBroker);

        Assert.Equal(1, messageBroker.GetSubscriberCount<NetworkRouteBattleEnemies>());

        messageBroker.Publish(this, new NetworkRouteBattleEnemies("map-event", 1));

        Assert.Equal(1, messageBroker.GetSubscriberCount<NetworkRouteBattleEnemies>());
    }

    private sealed class InspectableMessageBroker : MessageBroker
    {
        public int GetSubscriberCount<T>() =>
            subscribers.TryGetValue(typeof(T), out var subscriptions) ? subscriptions.Count : 0;
    }
}
