using Common;
using Common.Messaging;
using GameInterface.Services.Locations;
using GameInterface.Services.Locations.Conversations;
using GameInterface.Services.MapEvents;
using GameInterface.Services.ObjectManager;
using Missions;
using Missions.Agents;
using Missions.Agents.Handlers;
using Missions.Locations;
using Missions.Messages;
using Moq;
using System;
using System.Threading;

namespace E2E.Tests.Services.Locations;

/// <summary>Checks receiver-side location puppet lifecycle ordering.</summary>
public class LocationPuppetSpawnerTests
{
    [Fact]
    public void RemoteCompanionDespawn_EndsReceiverConversationBeforeRegistryRemoval()
    {
        using var broker = new MessageBroker();
        var agentId = Guid.NewGuid();
        var agentInfo = new CoopAgentInfo("host", "host", "host", null, agentId, 0);
        var registry = new Mock<INetworkAgentRegistry>();
        registry.Setup(value => value.TryGetAgentInfo(agentId, out agentInfo)).Returns(true);

        int order = 0;
        var conversationAgentGuard = new Mock<ILocationConversationAgentGuard>();
        conversationAgentGuard
            .Setup(value => value.EndConversationWithAgent(null))
            .Callback(() => Assert.Equal(0, order++));
        registry
            .Setup(value => value.RemoveAgent(agentId))
            .Callback(() => Assert.Equal(1, order++))
            .Returns(true);

        var movementHandler = new Mock<IAgentMovementHandler>();
        movementHandler.SetupGet(value => value.Interpolator).Returns(Mock.Of<IAgentPositionInterpolator>());
        var missionComponent = new Mock<ICoopMissionComponent>();
        missionComponent.SetupGet(value => value.AgentRegistry).Returns(registry.Object);
        missionComponent.SetupGet(value => value.AgentMovementHandler).Returns(movementHandler.Object);

        using var spawner = new LocationPuppetSpawner(
            broker,
            Mock.Of<IObjectManager>(),
            missionComponent.Object,
            Mock.Of<ILocationSession>(),
            conversationAgentGuard.Object,
            Mock.Of<ILocationAgentBindingMap>(),
            Mock.Of<ILocationPartyAgentMap>(),
            Mock.Of<ILocationPuppetRosterBinder>(),
            Mock.Of<IBattleAgentBudget>(),
            Mock.Of<ILocationAgentSpawnBatchCodec>(),
            Mock.Of<ILocationAuthorityMigrator>(),
            Mock.Of<ILocationControllerWithdrawalState>());

        int previousGameThreadId = GameThread.Instance.GameThreadId;
        GameThread.Instance.MarkGameThread();
        try
        {
            var networkThread = new Thread(() => broker.Publish(this, new NetworkDespawnLocationAgents(
                new[] { agentId },
                new[] { (byte)LocationDespawnReason.Removed },
                new[] { string.Empty })));
            networkThread.Start();
            networkThread.Join();

            GameThread.Instance.Update(TimeSpan.Zero);

            Assert.Equal(2, order);
            conversationAgentGuard.Verify(value => value.EndConversationWithAgent(null), Times.Once);
            registry.Verify(value => value.RemoveAgent(agentId), Times.Once);
        }
        finally
        {
            GameThread.Instance.DiscardQueuedActions();
            GameThread.Instance.RestoreGameThread(previousGameThreadId);
        }
    }
}
