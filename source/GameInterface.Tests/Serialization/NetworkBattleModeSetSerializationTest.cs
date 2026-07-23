using GameInterface.Services.MapEvents.Messages.Start;
using ProtoBuf.Meta;
using System.IO;
using Xunit;

namespace GameInterface.Tests.Serialization;

public class NetworkBattleModeSetSerializationTest
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void RoundTrip_PreservesFields(int mode)
    {
        var original = new NetworkBattleModeSet("mapEvent-7", mode);

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
        Assert.Equal(mode, result.Mode);
    }
}
