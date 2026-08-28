using Common.Messaging;
using GameInterface.Services.GameDebug.Messages;
using Missions.Battles;

namespace E2E.Tests.Services.Missions;

public class BattleDebugRouteHandlerTests
{
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
