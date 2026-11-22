using Common.Serialization;
using GameInterface.Services.PartyVisuals.Messages;
using Xunit;

namespace GameInterface.Tests.Serialization;

/// <summary>Checks the create message against the wire layout used by both previous server paths.</summary>
public class PartyVisualSerializationTests
{
    [Fact]
    public void CreateVisual_UsesOnlyTheExistingPartyHandleField()
    {
        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
        var message = new NetworkCreatePartyVisual(300);
        byte[] legacyHeadless = { 0x08, 0xD1, 0xE0, 0x8E, 0x38, 0x12, 0x03, 0x18, 0xAC, 0x02 };
        byte[] legacyGraphical =
        {
            0x08, 0xD1, 0xE0, 0x8E, 0x38, 0x12, 0x08,
            0x0A, 0x01, 0x76, 0x10, 0x01, 0x18, 0xAC, 0x02,
        };

        var bytes = serializer.Serialize(message);

        Assert.Equal(legacyHeadless, bytes);
        Assert.Equal(message, serializer.Deserialize<NetworkCreatePartyVisual>(bytes));
        Assert.Equal(message, serializer.Deserialize<NetworkCreatePartyVisual>(legacyGraphical));
    }
}
