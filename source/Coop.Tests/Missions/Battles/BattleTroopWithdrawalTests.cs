using Missions;
using Missions.Battles;
using Moq;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class BattleTroopWithdrawalTests
{
    // M8c: a departing hull's crew withdraws at the removal step, before its party's despawn; the despawn then finds nothing,
    // so the withdrawn health is recorded once and no casualty attribution survives.
    [Fact]
    public void Deregister_RecordsTheHealthBeforeDeregistering_AndASecondWithdrawalRecordsNothing()
    {
        var agent = (Agent)RuntimeHelpers.GetUninitializedObject(typeof(Agent));
        var agentId = Guid.NewGuid();
        var info = new CoopAgentInfo("host", "host", "host", agent, agentId, 1);
        bool registered = true;
        var steps = new List<string>();
        var registry = new Mock<INetworkAgentRegistry>();
        registry.Setup(r => r.TryGetAgentInfo(agent, out info)).Returns(() => registered);
        registry.Setup(r => r.RemoveAgent(agentId)).Callback(() =>
        {
            registered = false;
            steps.Add("deregister");
        }).Returns(true);
        var component = new Mock<ICoopMissionComponent>();
        component.SetupGet(c => c.AgentRegistry).Returns(registry.Object);
        var lifecycle = new Mock<IBattleInstanceLifecycle>();
        lifecycle.Setup(l => l.RecordWithdrawnHealth(agent)).Callback(() => steps.Add("record"));
        var casualties = new Mock<ICasualtyAttributionMap>();
        var withdrawal = new BattleTroopWithdrawal(component.Object, lifecycle.Object, casualties.Object);

        bool first = withdrawal.Deregister(agent);
        bool second = withdrawal.Deregister(agent);

        Assert.True(first);
        Assert.False(second);
        Assert.Equal(new[] { "record", "deregister" }, steps);
        lifecycle.Verify(l => l.RecordWithdrawnHealth(It.IsAny<Agent>()), Times.Once);
        casualties.Verify(c => c.Forget(agentId), Times.Once);
    }
}
