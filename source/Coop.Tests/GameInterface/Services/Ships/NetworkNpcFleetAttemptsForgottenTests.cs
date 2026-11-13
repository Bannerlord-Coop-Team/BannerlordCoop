using GameInterface.Services.Ships.Messages;
using ProtoBuf;
using System.IO;
using Xunit;

namespace Coop.Tests.GameInterface.Services.Ships;

public class NetworkNpcFleetAttemptsForgottenTests
{
    [Fact]
    public void ForgottenAttempts_RoundTripOverTheWire()
    {
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, new NetworkNpcFleetAttemptsForgotten("MapEvent_1"));
        stream.Position = 0;

        var copy = Serializer.Deserialize<NetworkNpcFleetAttemptsForgotten>(stream);

        Assert.Equal("MapEvent_1", copy.InstanceId);
    }
}
