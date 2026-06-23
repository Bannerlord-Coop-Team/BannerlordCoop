using GameInterface.Services.MapEvents.Messages.Start;
using ProtoBuf.Meta;
using System.IO;
using Xunit;

namespace GameInterface.Tests.Serialization;

public class NetworkAttackMissionAttemptedSerializationTest
{
    [Fact]
    public void RoundTrip_PreservesMapEventAndAttackerIds()
    {
        var original = new NetworkAttackMissionAttempted("mapEvent-7", "attacker-42");

        byte[] bytes;
        using (var ms = new MemoryStream())
        {
            RuntimeTypeModel.Default.Serialize(ms, original);
            bytes = ms.ToArray();
        }

        Assert.NotEmpty(bytes);

        NetworkAttackMissionAttempted result;
        using (var ms = new MemoryStream(bytes))
        {
            result = (NetworkAttackMissionAttempted)RuntimeTypeModel.Default.Deserialize(ms, null, typeof(NetworkAttackMissionAttempted));
        }

        Assert.Equal(original.MapEventId, result.MapEventId);
        Assert.Equal("mapEvent-7", result.MapEventId);
        Assert.Equal(original.AttackerPartyId, result.AttackerPartyId);
        Assert.Equal("attacker-42", result.AttackerPartyId);
    }
}
