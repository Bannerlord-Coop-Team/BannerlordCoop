using Common.Serialization;
using ProtoBuf;
using Xunit;

namespace Common.Tests.Serialization;

public class ProtoBufSerializerTests
{
    private readonly ProtoBufSerializer serializer = new ProtoBufSerializer(new SerializableTypeMapper());

    [Fact]
    public void TryPeekType_ResolvesType_WithoutDeserializingPayload()
    {
        var data = serializer.Serialize(new PeekTestMessage { Value = "hello" });

        Assert.True(serializer.TryPeekType(data, out var type));
        Assert.Equal(typeof(PeekTestMessage), type);
    }

    [Fact]
    public void TryPeekType_MatchesFullDeserialization()
    {
        var data = serializer.Serialize(new PeekTestMessage { Value = "hello" });

        serializer.TryPeekType(data, out var peekedType);
        var message = serializer.Deserialize(data);

        Assert.Equal(message.GetType(), peekedType);
        Assert.Equal("hello", ((PeekTestMessage)message).Value);
    }

    [Fact]
    public void TryPeekType_ReturnsFalse_ForUnreadableData()
    {
        Assert.False(serializer.TryPeekType(new byte[] { 0xFF, 0xFF, 0xFF }, out var type));
        Assert.Null(type);
    }

    [ProtoContract]
    public class PeekTestMessage
    {
        [ProtoMember(1)]
        public string Value { get; set; }
    }
}
