using Common.Messaging;
using Common.PacketHandlers;
using Common.Serialization;
using ProtoBuf;

namespace Common.Tests.Serialization;

public class AggregateMessagePacketTests
{
    [ProtoContract(SkipConstructor = true)]
    public class AggregationProbeMessage : IMessage
    {
        [ProtoMember(1)]
        public int Value { get; set; }

        public AggregationProbeMessage(int value) { Value = value; }
    }

    /// <summary>
    /// The full aggregation wire path: inner messages serialized exactly as a bare send would
    /// (MessagePacket.Create), packed into an envelope, envelope round-tripped through the real
    /// serializer, and each inner payload decoded back to its message — order intact.
    /// </summary>
    [Fact]
    public void Envelope_RoundTripsInnerMessagesInOrder()
    {
        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());

        var payloads = Enumerable.Range(0, 5)
            .Select(i => MessagePacket.Create(new AggregationProbeMessage(i), serializer).Data)
            .ToArray();

        byte[] wire = serializer.Serialize(new AggregateMessagePacket(payloads));

        var received = serializer.Deserialize<IPacket>(wire);
        var envelope = Assert.IsType<AggregateMessagePacket>(received);

        Assert.Equal(5, envelope.Messages.Length);
        for (int i = 0; i < envelope.Messages.Length; i++)
        {
            var inner = serializer.Deserialize<IMessage>(envelope.Messages[i]);
            var probe = Assert.IsType<AggregationProbeMessage>(inner);
            Assert.Equal(i, probe.Value);
        }
    }

    [Fact]
    public void Envelope_UsesReliableOrderedOnMessageChannel()
    {
        // The envelope must ride the same delivery as the bare messages it replaces, or aggregation
        // would change cross-message ordering guarantees.
        var packet = new AggregateMessagePacket(Array.Empty<byte[]>());

        Assert.Equal(LiteNetLib.DeliveryMethod.ReliableOrdered, packet.DeliveryMethod);
        Assert.Equal(PacketType.AggregateMessage, packet.PacketType);
    }
}
