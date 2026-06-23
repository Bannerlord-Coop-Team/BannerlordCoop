using GameInterface.Services.MapEvents.Messages.Conversation;
using ProtoBuf.Meta;
using System.IO;
using Xunit;

namespace GameInterface.Tests.Serialization;

public class NetworkPlayerInteractionSerializationTest
{
    [Fact]
    public void Started_RoundTrip_PreservesFields()
    {
        var original = new NetworkPlayerInteractionStarted("defender-party-1", "Attacker McAttackface");

        var result = RoundTrip(original);

        Assert.Equal(original.DefenderPartyId, result.DefenderPartyId);
        Assert.Equal(original.AttackerName, result.AttackerName);
    }

    [Fact]
    public void Ended_RoundTrip_PreservesFields()
    {
        var original = new NetworkPlayerInteractionEnded("defender-party-1");

        var result = RoundTrip(original);

        Assert.Equal(original.DefenderPartyId, result.DefenderPartyId);
    }

    private static T RoundTrip<T>(T original)
    {
        byte[] bytes;
        using (var ms = new MemoryStream())
        {
            RuntimeTypeModel.Default.Serialize(ms, original);
            bytes = ms.ToArray();
        }

        Assert.NotEmpty(bytes);

        using (var ms = new MemoryStream(bytes))
        {
            return (T)RuntimeTypeModel.Default.Deserialize(ms, null, typeof(T));
        }
    }
}
