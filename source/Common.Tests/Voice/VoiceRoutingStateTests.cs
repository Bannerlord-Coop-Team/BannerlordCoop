using Common.Voice;
using ProtoBuf;
using Common.Serialization;
using LiteNetLib;
using Xunit;

namespace Common.Tests.Voice;

public class VoiceRoutingStateTests
{
    private VoicePosition Position(long epoch = 1, string context = "campaign", bool speak = true)
        => new(context, epoch, 0, 0, 0, speak, true);
    private VoicePacket Packet(long epoch = 1, uint sequence = 1, string context = "campaign", bool speak = true)
        => new() { Position = Position(epoch, context, speak), Sequence = sequence, StateSequence = sequence, Audio = new byte[] { 1 } };

    [Fact]
    public void AudioBeforeReliableContextIsDroppedAndDoesNotEstablishPermission()
    {
        var state = new VoiceRoutingState(new VoicePolicy());
        Assert.False(state.Update("a", Packet(), 0));
        Assert.True(state.ChangeContext("a", Position()));
        Assert.True(state.Update("a", Packet(sequence: 2), 10));
        Assert.False(state.Update("a", Packet(), 20));
    }

    [Fact]
    public void ContextTransitionRejectsOldAudioEvenWhenPositionArrivesFirst()
    {
        var state = new VoiceRoutingState(new VoicePolicy());
        state.ChangeContext("a", Position());
        Assert.False(state.Update("a", Packet(2, context: "scene:x"), 0));
        state.ChangeContext("a", Position(2, "scene:x"));
        Assert.False(state.Update("a", Packet(), 1));
        Assert.False(state.ChangeContext("a", Position()));
        Assert.True(state.Update("a", Packet(2, context: "scene:x"), 2));
    }

    [Fact]
    public void RequiresFreshListenerHeartbeatAndStampsListenerEpoch()
    {
        var state = new VoiceRoutingState(new VoicePolicy());
        state.ChangeContext("a", Position());
        state.ChangeContext("b", Position(2));
        var packet = Packet();
        state.Update("a", packet, 0);
        Assert.Empty(state.Route("a", packet, new VoiceRanges(), 0));
        state.Update("b", Packet(2, speak: false), 0);
        Assert.Equal(2, Assert.Single(state.Route("a", packet, new VoiceRanges(), 0)).ListenerEpoch);
        Assert.Empty(state.Route("a", packet, new VoiceRanges(), 501));
        state.Remove("b");
        Assert.Empty(state.Route("a", packet, new VoiceRanges(), 10));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(400, true)]
    [InlineData(401, false)]
    public void BoundsWirePayloadAndPreservesUnreliableDelivery(int length, bool valid)
    {
        var packet = Packet();
        packet.Audio = new byte[length];
        packet.Position = new VoicePosition(new string('x', 256), 7, 0, 0, 0, true, true);
        Assert.Equal(valid, packet.IsValid);
        Assert.Equal(DeliveryMethod.Unreliable, packet.DeliveryMethod);
        if (!valid) return;
        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
        var bytes = serializer.Serialize(packet);
        Assert.True(bytes.Length < 1200);
        Assert.True(serializer.Deserialize<VoicePacket>(bytes).IsValid);
        packet.Position = new VoicePosition(new string('x', 257), 7, 0, 0, 0, true, true);
        Assert.False(packet.IsValid);
        packet.Position = new VoicePosition("campaign", 0, 0, 0, 0, true, true);
        Assert.False(packet.IsValid);
        packet.Position = new VoicePosition("campaign", 1, float.PositiveInfinity, 0, 0, true, true);
        Assert.False(packet.IsValid);
    }

    [Fact]
    public void RoundTripsWireDataAndRejectsOversize()
    {
        var packet = Packet(42, uint.MaxValue, "scene:town:room");
        var copy = Serializer.DeepClone(packet);
        Assert.Equal(packet.Position.Context, copy.Position.Context);
        Assert.Equal(42, copy.Position.Epoch);
        Assert.Equal(uint.MaxValue, copy.Sequence);
        Assert.Equal(packet.Audio, copy.Audio);
        Assert.True(copy.IsValid);
        copy.Audio = new byte[VoicePacket.MaximumPayload + 1];
        Assert.False(copy.IsValid);
    }
}
