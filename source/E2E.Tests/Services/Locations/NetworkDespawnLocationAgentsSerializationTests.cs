using Missions.Messages;
using ProtoBuf;
using System;
using System.IO;
using Xunit;

namespace E2E.Tests.Services.Locations;

public class NetworkDespawnLocationAgentsSerializationTests
{
    [Fact]
    public void MixedPassageAndPlainDespawns_RoundTripParallelDestinations()
    {
        Guid passageAgentId = Guid.NewGuid();
        Guid deadAgentId = Guid.NewGuid();
        var original = new NetworkDespawnLocationAgents(
            new[] { passageAgentId, deadAgentId },
            new[] { (byte)LocationDespawnReason.Removed, (byte)LocationDespawnReason.Died },
            new[] { "destination_location", string.Empty });

        using var stream = new MemoryStream();
        Serializer.Serialize(stream, original);
        stream.Position = 0;

        var result = Serializer.Deserialize<NetworkDespawnLocationAgents>(stream);

        Assert.Equal(new[] { passageAgentId, deadAgentId }, result.AgentIds);
        Assert.Equal(
            new[] { (byte)LocationDespawnReason.Removed, (byte)LocationDespawnReason.Died },
            result.Reasons);
        Assert.Equal(new[] { "destination_location", string.Empty }, result.DestinationLocationIds);
    }
}
