using Common;
using Common.Messaging;
using GameInterface.Services.Locations.Conversations;
using Missions;
using Missions.Agents;
using Missions.Agents.Handlers;
using Missions.Locations;
using Missions.Messages;
using Missions.Services.Network;
using Moq;
using System;
using System.Threading;

namespace E2E.Tests.Services.Locations;

public class LocationAuthorityMigratorTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PeerDeparture_EndsConversationBeforePartyPuppetRemoval(bool disconnected)
    {
        const string instanceId = "location";
        const string controllerId = "departed";
        using var broker = new MessageBroker();
        var session = new Mock<ILocationSession>();
        session.SetupGet(value => value.InstanceId).Returns(instanceId);

        var agentId = Guid.NewGuid();
        var agentInfo = new CoopAgentInfo(controllerId, controllerId, controllerId, null, agentId, 0);
        var registry = new Mock<INetworkAgentRegistry>();
        registry.Setup(value => value.GetAgents(controllerId)).Returns(new[] { agentInfo });

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

        using var migrator = new LocationAuthorityMigrator(
            broker,
            missionComponent.Object,
            session.Object,
            Mock.Of<ILocationAgentBindingMap>(),
            Mock.Of<ILocationPartyAgentMap>(),
            Mock.Of<IMissionContext>(),
            Mock.Of<ILocationNpcHoldRegistry>(),
            conversationAgentGuard.Object);

        int previousGameThreadId = GameThread.Instance.GameThreadId;
        GameThread.Instance.MarkGameThread();
        try
        {
            var networkThread = new Thread(() =>
            {
                if (disconnected)
                    broker.Publish(this, new MissionPeerDisconnected(controllerId, instanceId));
                else
                    broker.Publish(this, new MissionPeerLeft(controllerId, instanceId));
            });
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
