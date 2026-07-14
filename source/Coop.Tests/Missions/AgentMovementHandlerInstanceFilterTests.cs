using System;
using Common.Messaging;
using Common.PacketHandlers;
using GameInterface.Services.Entity;
using Missions;
using Missions.Agents.Handlers;
using Missions.Messages;
using Missions.Services.Network;
using Moq;
using Xunit;

namespace Coop.Tests.Missions;

/// <summary>
/// Unit tests for <see cref="AgentMovementHandler"/>'s instance filtering of <see cref="NetworkMissionPeerEntered"/>.
/// The entry is the cue to clear a party a controller left behind on a missed departure — but only for OUR
/// instance. A stale or in-flight introduction for another instance would otherwise despawn a party that
/// legitimately belongs to the current one (matching the same filter on <see cref="MissionContext"/> and
/// <c>CoopMissionController</c>). The observable is the <see cref="INetworkAgentRegistry.GetAgents"/> lookup the
/// cleanup makes: it runs only when the introduction is acted on.
/// </summary>
public class AgentMovementHandlerInstanceFilterTests
{
    private const string CurrentInstance = "instance1";

    private readonly MessageBroker messageBroker = new();
    private readonly Mock<IBattleNetwork> network = new();
    private readonly Mock<IPacketManager> packetManager = new();
    private readonly Mock<INetworkAgentRegistry> agentRegistry = new();
    private readonly Mock<IControllerIdProvider> controllerIdProvider = new();
    private readonly Mock<IMissionContext> missionContext = new();
    private readonly AgentMovementHandler handler;

    public AgentMovementHandlerInstanceFilterTests()
    {
        controllerIdProvider.SetupGet(p => p.ControllerId).Returns("us");
        missionContext.SetupGet(c => c.CurrentInstanceId).Returns(CurrentInstance);
        agentRegistry.Setup(r => r.GetAgents(It.IsAny<string>())).Returns(Array.Empty<CoopAgentInfo>());

        handler = new AgentMovementHandler(
            network.Object,
            packetManager.Object,
            messageBroker,
            agentRegistry.Object,
            controllerIdProvider.Object,
            missionContext.Object);
    }

    private void Introduce(string controllerId, string instanceId) =>
        messageBroker.Publish(this, new NetworkMissionPeerEntered(controllerId, instanceId));

    [Fact]
    public void PeerEntered_ForTheCurrentInstance_RunsTheStaleCleanup()
    {
        Introduce("them", CurrentInstance);

        agentRegistry.Verify(r => r.GetAgents("them"), Times.Once);
    }

    [Fact]
    public void PeerEntered_ForAnotherInstance_IsIgnored()
    {
        // A stale/in-flight introduction for a different instance must not despawn a party that belongs to the
        // current one — so the cleanup never runs.
        Introduce("them", "instance2");

        agentRegistry.Verify(r => r.GetAgents(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void PeerEntered_WithoutAnInstanceId_IsAccepted()
    {
        // Null is tolerated as a wildcard for locally published legacy/test messages — the server fan-out
        // always carries the instance id.
        Introduce("them", null);

        agentRegistry.Verify(r => r.GetAgents("them"), Times.Once);
    }

    [Fact]
    public void PeerEntered_ForOwnController_IsIgnored()
    {
        // Our own party is managed locally; the cleanup short-circuits before the registry lookup.
        Introduce("us", CurrentInstance);

        agentRegistry.Verify(r => r.GetAgents(It.IsAny<string>()), Times.Never);
    }
}
