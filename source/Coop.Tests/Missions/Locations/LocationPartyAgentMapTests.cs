using Missions.Locations;
using System;
using Xunit;

namespace Coop.Tests.Missions.Locations;

public class LocationPartyAgentMapTests
{
    [Fact]
    public void RecordAndForget_TracksPartyIdentity()
    {
        var map = new LocationPartyAgentMap();
        Guid agentId = Guid.NewGuid();

        map.Record(agentId);
        Assert.True(map.Contains(agentId));

        map.Forget(agentId);
        Assert.False(map.Contains(agentId));
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    public void Migration_AdoptsOnlyBoundNonPartyAgents(
        bool hasNpcBinding,
        bool isPartyAgent,
        bool expected)
    {
        var map = new LocationPartyAgentMap();
        Guid agentId = Guid.NewGuid();
        if (isPartyAgent) map.Record(agentId);

        Assert.Equal(expected, map.ShouldAdoptAsNpc(agentId, hasNpcBinding));
    }
}
