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
    private const string InstanceId = "map-event-1";

    [Fact]
    public void Leave_ClearsEveryAgentRegistryIndex()
    {
        var registry = NewRegistry();
        var agent = ObjectHelper.SkipConstructor<Agent>();
        var agentId = Guid.NewGuid();
        Assert.True(registry.TryRegisterAgent(
            PeerController, PeerController, PeerController, agentId, 42, agent));

        using var lifecycle = NewLifecycle(registry);
        lifecycle.Leave();

        Assert.Empty(registry.GetControllerIds());
        Assert.Empty(registry.GetAgents(PeerController));
        Assert.False(registry.TryGetAgentInfo(agent, out _));
        Assert.False(registry.TryGetAgentInfo(agentId, out _));
        Assert.False(registry.TryGetAgentInfo(PeerController, 42, out _));
    }

    private static NetworkAgentRegistry NewRegistry()
    {
        var provider = new Mock<IControllerIdProvider>();
        provider.SetupGet(p => p.ControllerId).Returns(OwnController);
        return new NetworkAgentRegistry(provider.Object);
    }

    private static BattleInstanceLifecycle NewLifecycle(INetworkAgentRegistry registry)
    {
        var component = new Mock<ICoopMissionComponent>();
        component.SetupGet(c => c.AgentRegistry).Returns(registry);

        var session = new Mock<IBattleSession>();
        session.SetupGet(s => s.HasInstance).Returns(true);
        session.SetupGet(s => s.InstanceId).Returns(InstanceId);
        session.SetupGet(s => s.OwnControllerId).Returns(OwnController);

        return new BattleInstanceLifecycle(
            Mock.Of<IBattleNetwork>(),
            Mock.Of<INetwork>(),
            Mock.Of<IMessageBroker>(),
            Mock.Of<IObjectManager>(),
            component.Object,
            Mock.Of<INetworkWorldItemRegistry>(),
            session.Object,
            Mock.Of<IMissionContext>());
    }
}
