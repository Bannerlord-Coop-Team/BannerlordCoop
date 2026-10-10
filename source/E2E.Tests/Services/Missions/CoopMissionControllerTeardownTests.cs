using Common.Messaging;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.ObjectManager;
using LiteNetLib;
using Missions;
using Missions.Agents.Handlers;
using Missions.Messages;
using Missions.Missiles.Handlers;
using Moq;
using System;
using System.Collections.Generic;
using TaleWorlds.MountAndBlade;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// Tests for the guaranteed part of mission teardown, <see cref="CoopMissionController.OnEndMissionInternal"/>:
/// the mission's services are detached and the agent registry is emptied on every exit path, including the one
/// where an earlier teardown step throws. The agent registry is real and keyed by uninitialized
/// <see cref="Agent"/> instances, every other collaborator is a stub, so no live mission is needed.
/// </summary>
public class CoopMissionControllerTeardownTests
{
    private const string OwnController = "me";
    private const string PeerController = "peer";
    private const string OtherController = "other";

    [Fact]
    public void OnEndMissionInternal_ClearsEveryAgentRegistryIndex()
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

        using var controller = NewController(registry);
        controller.OnEndMissionInternal();

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
    public void OnEndMissionInternal_ClearsTheAgentRegistry_WhenAHandlerDisposalThrows()
    {
        var registry = NewRegistry();
        var agent = ObjectHelper.SkipConstructor<Agent>();
        var agentId = Guid.NewGuid();
        Assert.True(registry.TryRegisterAgent(
            PeerController, PeerController, PeerController, agentId, 42, agent));

        var movementHandler = new Mock<IAgentMovementHandler>();
        movementHandler.Setup(h => h.Dispose())
            .Throws(new InvalidOperationException("handler teardown failed"));
        var component = NewComponent(registry);
        component.SetupGet(c => c.AgentMovementHandler).Returns(movementHandler.Object);

        using var controller = new TeardownTestController(component.Object);

        Assert.Throws<InvalidOperationException>(() => controller.OnEndMissionInternal());

        Assert.Empty(registry.GetControllerIds());
        Assert.False(registry.TryGetAgentInfo(agent, out _));
        Assert.False(registry.TryGetAgentInfo(agentId, out _));
        Assert.False(registry.TryGetAgentInfo(PeerController, 42, out _));
    }

    [Fact]
    public void OnEndMissionInternal_ClearsTheAgentRegistry_WhenLeavingThrows()
    {
        var registry = NewRegistry();
        var agent = ObjectHelper.SkipConstructor<Agent>();
        var agentId = Guid.NewGuid();
        Assert.True(registry.TryRegisterAgent(
            PeerController, PeerController, PeerController, agentId, 42, agent));

        using var controller = NewController(
            registry,
            onLeaving: () => throw new InvalidOperationException("leave announcement failed"));

        Assert.Throws<InvalidOperationException>(() => controller.OnEndMissionInternal());

        Assert.Empty(registry.GetControllerIds());
        Assert.False(registry.TryGetAgentInfo(agent, out _));
        Assert.False(registry.TryGetAgentInfo(agentId, out _));
        Assert.False(registry.TryGetAgentInfo(PeerController, 42, out _));
    }

    [Fact]
    public void OnEndMissionInternal_DetachesTheMissionServices_BeforeItClearsTheRegistry()
    {
        var steps = new List<string>();
        var registry = new Mock<INetworkAgentRegistry>();
        registry.Setup(r => r.Clear()).Callback(() => steps.Add("clear"));

        using var controller = NewController(
            registry.Object,
            onLeaving: () => throw new InvalidOperationException("leave announcement failed"),
            onDispose: () => steps.Add("dispose"));

        Assert.Throws<InvalidOperationException>(() => controller.OnEndMissionInternal());

        // The spawn services that write to the registry are detached in Dispose, so detaching has to come
        // first; clearing first would let a still-subscribed spawn land an agent in the emptied registry.
        Assert.Equal(new[] { "dispose", "clear" }, steps);
    }

    [Fact]
    public void OnEndMissionInternal_CalledTwice_LeavesTheRegistryEmpty()
    {
        var registry = NewRegistry();
        Assert.True(registry.TryRegisterAgent(
            PeerController, Guid.NewGuid(), ObjectHelper.SkipConstructor<Agent>()));

        using var controller = NewController(registry);
        controller.OnEndMissionInternal();
        controller.OnEndMissionInternal();

        Assert.Empty(registry.GetControllerIds());
    }

    [Fact]
    public void SecondMission_DoesNotSeeTheAgentsOfTheFirst()
    {
        var registry = NewRegistry();
        var firstAgent = ObjectHelper.SkipConstructor<Agent>();
        var firstAgentId = Guid.NewGuid();
        Assert.True(registry.TryRegisterAgent(
            PeerController, PeerController, PeerController, firstAgentId, 42, firstAgent));

        using (var firstMission = NewController(registry))
        {
            firstMission.OnEndMissionInternal();
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

        using var secondMission = NewController(registry);
        secondMission.OnEndMissionInternal();

        Assert.Empty(registry.GetControllerIds());
    }

    private static NetworkAgentRegistry NewRegistry()
    {
        var provider = new Mock<IControllerIdProvider>();
        provider.SetupGet(p => p.ControllerId).Returns(OwnController);
        return new NetworkAgentRegistry(provider.Object);
    }

    private static Mock<ICoopMissionComponent> NewComponent(INetworkAgentRegistry registry)
    {
        var component = new Mock<ICoopMissionComponent>();
        component.SetupGet(c => c.AgentRegistry).Returns(registry);
        component.SetupGet(c => c.AgentMovementHandler).Returns(Mock.Of<IAgentMovementHandler>());
        component.SetupGet(c => c.AgentActionHandler).Returns(Mock.Of<IAgentActionHandler>());
        component.SetupGet(c => c.AgentVoiceHandler).Returns(Mock.Of<IAgentVoiceHandler>());
        component.SetupGet(c => c.MissileHandler).Returns(Mock.Of<IMissileHandler>());
        component.SetupGet(c => c.WeaponDropHandler).Returns(Mock.Of<IWeaponDropHandler>());
        component.SetupGet(c => c.WeaponPickupHandler).Returns(Mock.Of<IWeaponPickupHandler>());
        component.SetupGet(c => c.ShieldDamageHandler).Returns(Mock.Of<IShieldDamageHandler>());
        component.SetupGet(c => c.CombatHitPresentationHandler).Returns(Mock.Of<ICombatHitPresentationHandler>());
        component.SetupGet(c => c.AgentDeathHandler).Returns(Mock.Of<IAgentDeathHandler>());
        return component;
    }

    private static TeardownTestController NewController(
        INetworkAgentRegistry registry,
        Action onLeaving = null,
        Action onDispose = null)
        => new TeardownTestController(NewComponent(registry).Object, onLeaving, onDispose);

    /// <summary>
    /// The smallest concrete <see cref="CoopMissionController"/>: it carries the base teardown and nothing
    /// else, so a test can make any single teardown step throw.
    /// </summary>
    private sealed class TeardownTestController : CoopMissionController
    {
        private readonly Action onLeaving;
        private readonly Action onDispose;

        public TeardownTestController(
            ICoopMissionComponent coopMissionComponent,
            Action onLeaving = null,
            Action onDispose = null)
            : base(
                Mock.Of<IBattleNetwork>(),
                Mock.Of<IMessageBroker>(),
                Mock.Of<IObjectManager>(),
                coopMissionComponent,
                MovementCadenceProfile.Battle)
        {
            this.onLeaving = onLeaving;
            this.onDispose = onDispose;
        }

        protected override void SendJoinInfo(string controllerId) { }

        protected override void HandleJoinInfo(NetPeer peer, NetworkMissionJoinInfo joinInfo) { }

        protected override void OnLeaving() => onLeaving?.Invoke();

        public override void Dispose()
        {
            onDispose?.Invoke();
            base.Dispose();
        }
    }
}
