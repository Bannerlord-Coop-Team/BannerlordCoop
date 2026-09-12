using GameInterface.Services.MapEvents.Messages.Start;
using ProtoBuf.Meta;
using System.IO;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Serialization;

public class NetworkBattleJoinSerializationTest
{
    [Fact]
    public void Request_RoundTrip_PreservesFields()
    {
        var original = new NetworkRequestJoinBattle(
            "request-1",
            "map-event-2",
            "party-3",
            BattleSideEnum.Defender);

        byte[] bytes;
        using (var stream = new MemoryStream())
        {
            RuntimeTypeModel.Default.Serialize(stream, original);
            bytes = stream.ToArray();
        }

        NetworkRequestJoinBattle result;
        using (var stream = new MemoryStream(bytes))
        {
            result = (NetworkRequestJoinBattle)RuntimeTypeModel.Default.Deserialize(
                stream,
                null,
                typeof(NetworkRequestJoinBattle));
        }

        Assert.Equal("request-1", result.RequestId);
        Assert.Equal("map-event-2", result.MapEventId);
        Assert.Equal("party-3", result.PartyId);
        Assert.Equal(BattleSideEnum.Defender, result.Side);
    }

    [Fact]
    public void Reply_RoundTrip_PreservesFields()
    {
        var original = new NetworkJoinBattleReply(
            "request-1",
            "map-event-2",
            "party-3",
            accepted: true);

        byte[] bytes;
        using (var stream = new MemoryStream())
        {
            RuntimeTypeModel.Default.Serialize(stream, original);
            bytes = stream.ToArray();
        }

        NetworkJoinBattleReply result;
        using (var stream = new MemoryStream(bytes))
        {
            result = (NetworkJoinBattleReply)RuntimeTypeModel.Default.Deserialize(
                stream,
                null,
                typeof(NetworkJoinBattleReply));
        }

        Assert.Equal("request-1", result.RequestId);
        Assert.Equal("map-event-2", result.MapEventId);
        Assert.Equal("party-3", result.PartyId);
        Assert.True(result.Accepted);
    }
}
