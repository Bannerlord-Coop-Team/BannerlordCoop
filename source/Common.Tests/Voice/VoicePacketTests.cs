using Common.Voice;
using ProtoBuf;
using Xunit;

namespace Common.Tests.Voice;

public class VoicePacketTests
{
    private VoicePacket Packet() => new()
    {
        Position = new VoicePosition("campaign", 3, 0, 0, 0, true, true),
        Audio = new byte[] { 1 }, Speaker = "platform:1234", StreamGeneration = 42
    };

    [Fact]
    public void PlatformIdentityAndIndependentStreamGenerationRoundTrip()
    {
        var packet = Serializer.DeepClone(Packet());
        Assert.Equal("platform:1234", packet.Speaker);
        Assert.Equal(42, packet.StreamGeneration);
        Assert.Equal(3, packet.Position.Epoch);
        Assert.True(packet.IsValid);
    }

    [Fact]
    public void InvalidIdentityLengthAndGenerationAreRejected()
    {
        var packet = Packet();
        packet.Speaker = new string('x', 257);
        Assert.False(packet.IsValid);
        packet.Speaker = "platform:1234";
        packet.StreamGeneration = -1;
        Assert.False(packet.IsValid);
    }
}
