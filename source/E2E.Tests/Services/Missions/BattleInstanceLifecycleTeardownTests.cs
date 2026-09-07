using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.ObjectManager;
using Missions;
using Missions.Agents;
using Missions.Battles;
using Missions.Services.Network;
using Moq;
using System;
using TaleWorlds.MountAndBlade;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// Tests for the battle teardown step <see cref="BattleInstanceLifecycle.Leave"/>. The agent registry is real
/// and keyed by uninitialized <see cref="Agent"/> instances, every other collaborator is a stub, so no live
/// mission is needed.
/// </summary>
public class BattleInstanceLifecycleTeardownTests
{
    private const string OwnController = "me";
    private const string PeerController = "peer";
    private const string OtherController = "other";
    private const string InstanceId = "map-event-1";

    [Fact]
    public void Leave_ClearsEveryAgentRegistryIndex()
    {
        var registry = NewRegistry();
        var peerAgent = ObjectHelper.SkipConstructor<Agent>();
        var otherAgent = ObjectHelper.SkipConstructor<Agent>();
        var peerAgentId = Guid.NewGuid();
        var otherAgentId = Guid.NewGuid();
        Assert.True(registry.TryRegisterAgent(
            PeerController, PeerController, PeerController, peerAgentId, 42, peerAgent));
        Assert.True(registry.TryRegisterAgent(
            OtherController, OtherController, OtherController, otherAgentId, 43, otherAgent));

        using var lifecycle = NewLifecycle(registry);
        lifecycle.Leave();

        Assert.Empty(registry.GetControllerIds());
        Assert.Empty(registry.GetAgents(PeerController));
        Assert.Empty(registry.GetAgents(OtherController));
        Assert.False(registry.TryGetAgentInfo(peerAgent, out _));
        Assert.False(registry.TryGetAgentInfo(otherAgent, out _));
        Assert.False(registry.TryGetAgentInfo(peerAgentId, out _));
        Assert.False(registry.TryGetAgentInfo(otherAgentId, out _));
        Assert.False(registry.TryGetAgentInfo(PeerController, 42, out _));
        Assert.False(registry.TryGetAgentInfo(OtherController, 43, out _));
    }

    [Fact]
    public void Leave_ClearsTheAgentRegistry_WhenTheLeaveAnnouncementThrows()
    {
        var registry = NewRegistry();
        var agent = ObjectHelper.SkipConstructor<Agent>();
        var agentId = Guid.NewGuid();
        Assert.True(registry.TryRegisterAgent(
            PeerController, PeerController, PeerController, agentId, 42, agent));

        var relay = new Mock<INetwork>();
        relay.Setup(n => n.SendAll(It.IsAny<IMessage>()))
            .Throws(new InvalidOperationException("leave announcement failed"));

        using var lifecycle = NewLifecycle(registry, relay.Object);

        Assert.Throws<InvalidOperationException>(() => lifecycle.Leave());

        Assert.Empty(registry.GetControllerIds());
        Assert.False(registry.TryGetAgentInfo(agent, out _));
        Assert.False(registry.TryGetAgentInfo(agentId, out _));
        Assert.False(registry.TryGetAgentInfo(PeerController, 42, out _));
    }

    [Fact]
    public void Leave_ClearsTheAgentRegistry_WhenAStepAfterTheSocketStopThrows()
    {
        var registry = NewRegistry();
        var agent = ObjectHelper.SkipConstructor<Agent>();
        var agentId = Guid.NewGuid();
        Assert.True(registry.TryRegisterAgent(
            PeerController, PeerController, PeerController, agentId, 42, agent));

        var worldItems = new Mock<INetworkWorldItemRegistry>();
        worldItems.Setup(w => w.Clear())
            .Throws(new InvalidOperationException("world item teardown failed"));

        using var lifecycle = NewLifecycle(registry, worldItemRegistry: worldItems.Object);

        Assert.Throws<InvalidOperationException>(() => lifecycle.Leave());

        Assert.Empty(registry.GetControllerIds());
        Assert.False(registry.TryGetAgentInfo(agent, out _));
        Assert.False(registry.TryGetAgentInfo(agentId, out _));
        Assert.False(registry.TryGetAgentInfo(PeerController, 42, out _));
    }

    [Fact]
    public void Leave_CalledTwice_LeavesTheRegistryEmpty()
    {
        var registry = NewRegistry();
        Assert.True(registry.TryRegisterAgent(
            PeerController, Guid.NewGuid(), ObjectHelper.SkipConstructor<Agent>()));

        using var lifecycle = NewLifecycle(registry);
        lifecycle.Leave();
        lifecycle.Leave();

        Assert.Empty(registry.GetControllerIds());
    }

    [Fact]
    public void SecondBattle_DoesNotSeeTheAgentsOfTheFirst()
    {
        var registry = NewRegistry();
        var firstAgent = ObjectHelper.SkipConstructor<Agent>();
        var firstAgentId = Guid.NewGuid();
        Assert.True(registry.TryRegisterAgent(
            PeerController, PeerController, PeerController, firstAgentId, 42, firstAgent));

        using (var firstBattle = NewLifecycle(registry))
        {
            firstBattle.Leave();
        }

        var secondAgent = ObjectHelper.SkipConstructor<Agent>();
        var secondAgentId = Guid.NewGuid();
        Assert.True(registry.TryRegisterAgent(
            PeerController, PeerController, PeerController, secondAgentId, 42, secondAgent));

        Assert.True(registry.TryGetAgentInfo(PeerController, 42, out var resolved));
        Assert.Same(secondAgent, resolved.Agent);
        Assert.Equal(secondAgentId, resolved.AgentId);
        Assert.False(registry.TryGetAgentInfo(firstAgent, out _));
        Assert.False(registry.TryGetAgentInfo(firstAgentId, out _));

        using var secondBattle = NewLifecycle(registry);
        secondBattle.Leave();

        Assert.Empty(registry.GetControllerIds());
    }

    private static NetworkAgentRegistry NewRegistry()
    {
        var provider = new Mock<IControllerIdProvider>();
        provider.SetupGet(p => p.ControllerId).Returns(OwnController);
        return new NetworkAgentRegistry(provider.Object);
    }

    private static BattleInstanceLifecycle NewLifecycle(
        INetworkAgentRegistry registry,
        INetwork relayNetwork = null,
        INetworkWorldItemRegistry worldItemRegistry = null)
    {
        var component = new Mock<ICoopMissionComponent>();
        component.SetupGet(c => c.AgentRegistry).Returns(registry);

        var session = new Mock<IBattleSession>();
        session.SetupGet(s => s.HasInstance).Returns(true);
        session.SetupGet(s => s.InstanceId).Returns(InstanceId);
        session.SetupGet(s => s.OwnControllerId).Returns(OwnController);

        return new BattleInstanceLifecycle(
            Mock.Of<IBattleNetwork>(),
            relayNetwork ?? Mock.Of<INetwork>(),
            Mock.Of<IMessageBroker>(),
            Mock.Of<IObjectManager>(),
            component.Object,
            worldItemRegistry ?? Mock.Of<INetworkWorldItemRegistry>(),
            session.Object,
            Mock.Of<IMissionContext>());
    }
}
