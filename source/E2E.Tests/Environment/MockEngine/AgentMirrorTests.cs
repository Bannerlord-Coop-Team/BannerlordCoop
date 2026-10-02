using Common.Util;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace E2E.Tests.Environment.MockEngine;

public class AgentMirrorTests
{
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