using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Surrogates;
using ProtoBuf.Meta;
using System;
using System.IO;
using Xunit;

namespace GameInterface.Tests.Serialization;

public class NetworkStartSiegeMissionSerializationTest
{
    public NetworkStartSiegeMissionSerializationTest()
    {
        new SurrogateCollection();
    }

    [Fact]
    public void RoundTrip_PreservesBattleSize()
    {
        var original = new NetworkStartSiegeMission(
            "map-event-1", 3, Array.Empty<float>(), Array.Empty<SiegeEngineState>(), Array.Empty<SiegeEngineState>(),
            "player-party-1", 850);

        NetworkStartSiegeMission result;
        using (var stream = new MemoryStream())
        {
            RuntimeTypeModel.Default.Serialize(stream, original);
            stream.Position = 0;
            result = (NetworkStartSiegeMission)RuntimeTypeModel.Default.Deserialize(
                stream, null, typeof(NetworkStartSiegeMission));
        }

        Assert.Equal("map-event-1", result.MapEventId);
        Assert.Equal(3, result.WallLevel);
        Assert.Equal("player-party-1", result.InitiatingPartyId);
        Assert.Equal(850, result.BattleSize);
    }
}
