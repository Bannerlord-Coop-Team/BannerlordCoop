using GameInterface.Services.PartyVisuals.Messages;
using ProtoBuf.Meta;
using System.IO;
using Xunit;

namespace GameInterface.Tests.Serialization;

public class NetworkCreatePartyVisualSerializationTest
{
    [Fact]
    public void RoundTrip_PreservesIds()
    {
        var original = new NetworkCreatePartyVisual("MobilePartyVisual_42", "MobileParty_Created_7");

        byte[] bytes;
        using (var ms = new MemoryStream())
        {
            RuntimeTypeModel.Default.Serialize(ms, original);
            bytes = ms.ToArray();
        }

        Assert.NotEmpty(bytes);

        NetworkCreatePartyVisual result;
        using (var ms = new MemoryStream(bytes))
        {
            result = (NetworkCreatePartyVisual)RuntimeTypeModel.Default.Deserialize(ms, null, typeof(NetworkCreatePartyVisual));
        }

        Assert.Equal(original.PartyVisualId, result.PartyVisualId);
        Assert.Equal(original.MobilePartyId, result.MobilePartyId);
        Assert.Equal("MobilePartyVisual_42", result.PartyVisualId);
        Assert.Equal("MobileParty_Created_7", result.MobilePartyId);
    }
}
