using Missions.Messages;
using ProtoBuf;
using System;
using System.IO;
using Xunit;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// Verifies battle-host assignments preserve a usable successor list across protobuf serialization.
/// </summary>
public class NetworkBattleHostAssignedSerializationTests
{
    [Fact]
    public void EmptySuccessorList_RoundTripsAsEmptyArray()
    {
        var original = new NetworkBattleHostAssigned("map-event", "host", Array.Empty<string>(), 1);

        using var stream = new MemoryStream();
        Serializer.Serialize(stream, original);
        stream.Position = 0;

        var result = Serializer.Deserialize<NetworkBattleHostAssigned>(stream);

        Assert.NotNull(result.SuccessorControllerIds);
        Assert.Empty(result.SuccessorControllerIds);
    }
}
