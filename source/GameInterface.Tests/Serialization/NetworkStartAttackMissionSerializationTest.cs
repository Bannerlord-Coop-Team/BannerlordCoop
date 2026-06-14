using GameInterface.Services.MapEvents.Messages.Start;
using ProtoBuf.Meta;
using System.IO;
using Xunit;

namespace GameInterface.Tests.Serialization;

public class NetworkStartAttackMissionSerializationTest
{
    [Fact]
    public void RoundTrip_PreservesRandomTerrainSeed()
    {
        var original = new NetworkStartAttackMission(4242);

        byte[] bytes;
        using (var ms = new MemoryStream())
        {
            RuntimeTypeModel.Default.Serialize(ms, original);
            bytes = ms.ToArray();
        }

        Assert.NotEmpty(bytes);

        NetworkStartAttackMission result;
        using (var ms = new MemoryStream(bytes))
        {
            result = (NetworkStartAttackMission)RuntimeTypeModel.Default.Deserialize(ms, null, typeof(NetworkStartAttackMission));
        }

        Assert.Equal(original.RandomTerrainSeed, result.RandomTerrainSeed);
        Assert.Equal(4242, result.RandomTerrainSeed);
    }
}
