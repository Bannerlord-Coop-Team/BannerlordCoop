using Common.Serialization;
using GameInterface.Services.PartyVisuals.Messages;
using Xunit;

namespace GameInterface.Tests.Serialization;

/// <summary>Checks that a replicated visual create keeps its party handle.</summary>
public class PartyVisualSerializationTests
{
    [Fact]
    public void CreateVisual_RoundTripsPartyHandle()
    {
        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
        var message = new NetworkCreatePartyVisual(300);

        var bytes = serializer.Serialize(message);

        Assert.Equal(message, serializer.Deserialize<NetworkCreatePartyVisual>(bytes));
    }
}
