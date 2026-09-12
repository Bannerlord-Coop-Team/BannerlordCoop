using Missions.Locations;
using System;
using Xunit;

namespace Coop.Tests.Missions.Locations;

public class LocationPartyAgentMapTests
{
    [Fact]
    public void Record_TracksPartyIdentityForMissionLifetime()
    {
        var map = new LocationPartyAgentMap();
        Guid agentId = Guid.NewGuid();

        map.Record(agentId);
        map.Record(agentId);

        Assert.True(map.Contains(agentId));
        Assert.False(map.Contains(Guid.Empty));
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
