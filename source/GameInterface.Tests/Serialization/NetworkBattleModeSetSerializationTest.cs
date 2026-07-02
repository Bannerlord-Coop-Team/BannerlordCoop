using GameInterface.Services.MapEvents.Messages.Start;
using ProtoBuf.Meta;
using System.IO;
using Xunit;

namespace GameInterface.Tests.Serialization;

public class NetworkBattleModeSetSerializationTest
{
    [Fact]
    public void RoundTrip_PreservesFields()
    {
        var original = new NetworkBattleModeSet("mapEvent-7", 1);

        byte[] bytes;
        using (var ms = new MemoryStream())
        {
            RuntimeTypeModel.Default.Serialize(ms, original);
            bytes = ms.ToArray();
        }

        Assert.NotEmpty(bytes);

        NetworkBattleModeSet result;
        using (var ms = new MemoryStream(bytes))
        {
            result = (NetworkBattleModeSet)RuntimeTypeModel.Default.Deserialize(ms, null, typeof(NetworkBattleModeSet));
        }

        Assert.Equal("mapEvent-7", result.MapEventId);
        Assert.Equal(1, result.Mode);
    }
}
