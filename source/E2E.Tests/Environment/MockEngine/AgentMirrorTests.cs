using Common.Util;
using TaleWorlds.MountAndBlade;
using Missions.Agents.Packets;
using TaleWorlds.Library;
using Xunit;

namespace E2E.Tests.Environment.MockEngine;

public class AgentMirrorTests
{
    [Theory]
    [InlineData(0f, 1)]
    [InlineData(1f, 0)]
    public void MountMovementUsesMirrorSetterAndPreservesUnchangedDirection(float initialX, int expectedWrites)
    {
        using var engine = new MissionEngineFixture();
        var sender = ObjectHelper.SkipConstructor<Agent>();
        var receiver = ObjectHelper.SkipConstructor<Agent>();
        var senderMirror = new MirrorAgent { MovementDirection = new Vec2(1f, 0f) };
        var receiverMirror = new MirrorAgent { MovementDirection = new Vec2(initialX, 0f) };
        AgentMirror.Bind(sender, senderMirror);
        AgentMirror.Bind(receiver, receiverMirror);

        new AgentMountData(sender).ApplyMount(receiver);

        Assert.Equal(new Vec2(1f, 0f), receiverMirror.MovementDirection);
        Assert.Equal(expectedWrites, receiverMirror.SetMovementDirectionCalls);
    }

    [Theory]
    [InlineData(100f)]
    [InlineData(17f)]
    [InlineData(0f)]
    public void HealthUsesTheAgentBackingField(float health)
    {
        var agent = ObjectHelper.SkipConstructor<Agent>();
        var mirror = new MirrorAgent();
        AgentMirror.Bind(agent, mirror);
        Assert.Equal(100f, agent._health);

        mirror.Health = health;
        Assert.Equal(health, agent._health);

        agent._health = 29f;
        Assert.Equal(29f, mirror.Health);
    }
}
