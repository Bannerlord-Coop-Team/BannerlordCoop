using Common.Tests.Utils;
using GameInterface.Services.GameDebug.Messages;
using Missions.Battles;

namespace E2E.Tests.Services.Missions;

public class BattleDebugRouteHandlerTests
{
    [Fact]
    public void RouteMessage_KeepsWeakSubscriptionAlive()
    {
        using var messageBroker = new TestMessageBroker();
        using var handler = new BattleDebugRouteHandler(messageBroker);

        Assert.Equal(1, messageBroker.GetSubscriberCountForType<NetworkRouteBattleEnemies>());

        messageBroker.Publish(this, new NetworkRouteBattleEnemies("map-event", 1));

        Assert.Equal(1, messageBroker.GetSubscriberCountForType<NetworkRouteBattleEnemies>());
    }
}
