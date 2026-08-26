using Common;
using Common.Messaging;
using GameInterface.Services.Locations;
using GameInterface.Services.Locations.Conversations;
using GameInterface.Services.MapEvents;
using GameInterface.Services.ObjectManager;
using Missions;
using Missions.Locations;
using Missions.Messages;
using Moq;
using System;
using System.Threading;

namespace E2E.Tests.Services.Locations;

public class LocationControllerWithdrawalStateTests
{
    [Fact]
    public void FormerHostRecord_SurvivesReentry_ButFreshRecordDoesNot()
    {
        var state = new LocationControllerWithdrawalState();
        var retainedAgentId = System.Guid.NewGuid();
        var freshAgentId = System.Guid.NewGuid();

        state.MarkWithdrawn("host-a", wasHost: true);
        Assert.True(state.IsRetainedFormerHostRecord("host-a", retainedAgentId));
        state.MarkEntered("host-a");

        Assert.False(state.IsWithdrawn("host-a", out bool wasFormerHost));
        Assert.False(wasFormerHost);
        Assert.True(state.IsRetainedFormerHostRecord("host-a", retainedAgentId));
        Assert.False(state.IsRetainedFormerHostRecord("host-a", freshAgentId));
    }

    [Fact]
    public void BufferedFormerHostRecord_MarkedAtWithdrawal_SurvivesReentry()
    {
        var state = new LocationControllerWithdrawalState();
        var retainedAgentId = System.Guid.NewGuid();

        state.MarkWithdrawn("host-a", wasHost: true);
        state.RetainFormerHostRecord(retainedAgentId);
        state.MarkEntered("host-a");

        Assert.True(state.IsRetainedFormerHostRecord("host-a", retainedAgentId));
    }

    [Fact]
    public void ReenteredPlainPlayer_IsNoLongerWithdrawn()
    {
        var state = new LocationControllerWithdrawalState();

        state.MarkWithdrawn("player", wasHost: false);
        state.MarkEntered("player");

        Assert.False(state.IsWithdrawn("player", out bool wasFormerHost));
        Assert.False(wasFormerHost);
    }

    [Fact]
    public void PeerEnterThenDisconnect_AppliesWithdrawalLastOnGameThread()
    {
        const string instanceId = "location";
        const string controllerId = "host-a";
        using var broker = new MessageBroker();
        var session = new Mock<ILocationSession>();
        session.SetupGet(value => value.InstanceId).Returns(instanceId);
        session.Setup(value => value.IsHostController(controllerId)).Returns(true);
        var state = new LocationControllerWithdrawalState();
        using var spawner = new LocationPuppetSpawner(
            broker,
            Mock.Of<IObjectManager>(),
            Mock.Of<ICoopMissionComponent>(),
            session.Object,
            Mock.Of<ILocationConversationAgentGuard>(),
            Mock.Of<ILocationAgentBindingMap>(),
            Mock.Of<ILocationPartyAgentMap>(),
            Mock.Of<ILocationPuppetRosterBinder>(),
            Mock.Of<IBattleAgentBudget>(),
            Mock.Of<ILocationAgentSpawnBatchCodec>(),
            Mock.Of<ILocationAuthorityMigrator>(),
            state);

        int previousGameThreadId = GameThread.Instance.GameThreadId;
        GameThread.Instance.MarkGameThread();
        try
        {
            var networkThread = new Thread(() =>
            {
                broker.Publish(this, new NetworkMissionPeerEntered(controllerId, instanceId));
                broker.Publish(this, new MissionPeerDisconnected(controllerId, instanceId));
            });
            networkThread.Start();
            networkThread.Join();

            GameThread.Instance.Update(TimeSpan.Zero);

            Assert.True(state.IsWithdrawn(controllerId, out bool wasFormerHost));
            Assert.True(wasFormerHost);
        }
        finally
        {
            GameThread.Instance.DiscardQueuedActions();
            GameThread.Instance.RestoreGameThread(previousGameThreadId);
        }
    }
}
