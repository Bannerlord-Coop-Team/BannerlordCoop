using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.Players.Data;
using ProtoBuf;
using System.IO;
using Xunit;

namespace Coop.Tests.Server.Connections.Messages;

public class NetworkSessionJoinedTests
{
    [Fact]
    public void NetworkSessionJoined_ProtobufRoundTrip()
    {
        var message = new NetworkSessionJoined(true, null, "session:abc", "test.mod", new byte[] { 9, 8, 7 },
            new Player("controller-1", null, null, null, null));

        var copy = RoundTrip(message);

        Assert.True(copy.Accepted);
        Assert.Equal("session:abc", copy.SessionId);
        Assert.Equal("test.mod", copy.ModId);
        Assert.Equal(new byte[] { 9, 8, 7 }, copy.Payload);
        Assert.Equal("controller-1", copy.Player.ControllerId);
        Assert.Null(copy.Player.HeroId);
    }

    [Fact]
    public void NetworkSessionJoined_Refusal_RoundTripsReason()
    {
        var copy = RoundTrip(new NetworkSessionJoined(false, "The co-op session has ended", null, null, null, null));

        Assert.False(copy.Accepted);
        Assert.Equal("The co-op session has ended", copy.Reason);
        Assert.Null(copy.Player);
    }

    private static NetworkSessionJoined RoundTrip(NetworkSessionJoined message)
    {
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, message);
        stream.Position = 0;
        return Serializer.Deserialize<NetworkSessionJoined>(stream);
    }
}
