using Common.Messaging;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.ObjectManager;
using Missions;
using Missions.Battles;
using Moq;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>M7: the naval pending queue rechecks its waiting troops every tick, so the recheck must stay one registry pass.</summary>
public class ReinforcementFielderLateReplayTests
{
    // A waiting batch reads the agent registry once, not once per troop, so a large recovery doesn't scale with agents per troop.
    [Fact]
    public void RemoveLateReplayed_EnumeratesTheRegistryOncePerBatch()
    {
        var registry = new Mock<INetworkAgentRegistry>();
        registry.Setup(r => r.GetControllerIds()).Returns(new[] { "host" });
        registry.Setup(r => r.GetAgents("host")).Returns(Array.Empty<CoopAgentInfo>());
        var component = new Mock<ICoopMissionComponent>();
        component.SetupGet(c => c.AgentRegistry).Returns(registry.Object);
        using var fielder = new ReinforcementFielder(Mock.Of<IMessageBroker>(), Mock.Of<IObjectManager>(), component.Object,
            Mock.Of<IBattleSession>(), Mock.Of<IBattleDeploymentCoordinator>(), Mock.Of<IAgentFormationAssigner>(),
            Mock.Of<ICasualtyAttributionMap>(), Mock.Of<IBattleAgentBudget>());
        var waiting = new List<CoopAgentOrigin> { Origin(), Origin(), Origin() };

        Assert.Equal(0, fielder.RemoveLateReplayed(waiting));

        Assert.Equal(3, waiting.Count);
        registry.Verify(r => r.GetControllerIds(), Times.Once);
        registry.Verify(r => r.GetAgents("host"), Times.Once);
    }

    // A recovered origin with no descriptor or party; only the batch's registry reads matter here.
    private static CoopAgentOrigin Origin() => (CoopAgentOrigin)RuntimeHelpers.GetUninitializedObject(typeof(CoopAgentOrigin));
}
