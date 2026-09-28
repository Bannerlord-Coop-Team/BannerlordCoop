using Common.Serialization;

namespace Common.Tests.Serialization;

/// <summary>Ownership of the pooled scratch buffer behind <see cref="ProtoBufSerializer.Serialize"/>.</summary>
public class ProtoBufSerializerBufferTests
{
    private static readonly SerializableTypeMapper TypeMapper = new SerializableTypeMapper();
    private static readonly LegacyProtoBufSerializer Legacy = new LegacyProtoBufSerializer(TypeMapper);

    public static IEnumerable<object[]> BlobLengths => new[] { 0, 100, 4090, 10_000, 300_000 }.Select(length => new object[] { length });

    [Theory]
    [MemberData(nameof(BlobLengths))]
    public void Serialize_ReturnsAnExactArrayThatIsNeverPooled(int blobLength)
    {
        var pool = new TrackingArrayPool();
        var serializer = new ProtoBufSerializer(TypeMapper, pool);
        var payload = new ProtoBufSerializerEquivalenceTests.BlobContract { Blob = new byte[blobLength] };

        byte[] actual = serializer.Serialize(payload);

        Assert.False(pool.EverRented(actual));
        Assert.Equal(Legacy.Serialize(payload), actual);
        Assert.Equal(pool.RentCount, pool.ReturnCount);
        Assert.Empty(pool.Outstanding);
    }

    [Fact]
    public void Serialize_ResultIsUnchangedWhenThePoolReusesTheBuffer()
    {
        var pool = new TrackingArrayPool(reuse: true);
        var serializer = new ProtoBufSerializer(TypeMapper, pool);
        var first = new ProtoBufSerializerEquivalenceTests.BlobContract { Blob = Enumerable.Repeat((byte)0x11, 6000).ToArray() };
        var second = new ProtoBufSerializerEquivalenceTests.BlobContract { Blob = Enumerable.Repeat((byte)0x22, 6000).ToArray() };
        byte[] expected = Legacy.Serialize(first);

        byte[] firstBytes = serializer.Serialize(first);
        byte[] copy = firstBytes.ToArray();
        serializer.Serialize(second);

        Assert.Equal(expected, copy);
        Assert.Equal(copy, firstBytes);
    }

    [Fact]
    public void Serialize_ThrowingMemberReturnsTheBufferAndNextCallMatches()
    {
        var pool = new TrackingArrayPool();
        var serializer = new ProtoBufSerializer(TypeMapper, pool);

        Assert.Throws<InvalidOperationException>(() => serializer.Serialize(new ProtoBufSerializerEquivalenceTests.ThrowingContract()));

        Assert.True(pool.RentCount > 0);
        Assert.Empty(pool.Outstanding);
        var next = new ProtoBufSerializerEquivalenceTests.NestedLeaf { Value = 3, Text = "after" };
        Assert.Equal(Legacy.Serialize(next), serializer.Serialize(next));
        Assert.Empty(pool.Outstanding);
    }

    [Fact]
    public void Serialize_ReentrantCallRentsItsOwnBuffer()
    {
        var pool = new TrackingArrayPool();
        var serializer = new ProtoBufSerializer(TypeMapper, pool);
        ProtoBufSerializerEquivalenceTests.ReentrantContract.Serialize = Legacy.Serialize;
        byte[] expected = Legacy.Serialize(new ProtoBufSerializerEquivalenceTests.ReentrantContract());

        ProtoBufSerializerEquivalenceTests.ReentrantContract.Serialize = serializer.Serialize;
        byte[] actual = serializer.Serialize(new ProtoBufSerializerEquivalenceTests.ReentrantContract());

        Assert.Equal(expected, actual);
        Assert.Equal(2, pool.RentCount);
        Assert.Empty(pool.Outstanding);
    }

    [Fact]
    public void Serialize_AllocatesLittleMoreThanTheOutputAfterWarmup()
    {
        var serializer = new ProtoBufSerializer(TypeMapper);
        object payload = new ProtoBufSerializerEquivalenceTests.NestedLeaf { Value = 42, Text = "movement" };
        int outputLength = 0;
        for (int i = 0; i < 20; i++) outputLength = serializer.Serialize(payload).Length;

        const int calls = 200;
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < calls; i++) serializer.Serialize(payload);
        long perCall = (GC.GetAllocatedBytesForCurrentThread() - before) / calls;

        // The legacy path allocated about 2.6 KB more per call: two MemoryStreams, two ToArray copies and the object dispatch.
        Assert.InRange(perCall, outputLength, outputLength + 256);
    }

    [Fact]
    public void Serialize_UnmappedTypeThrowsBeforeRenting()
    {
        var pool = new TrackingArrayPool();
        var serializer = new ProtoBufSerializer(new UnmappedTypeMapper(), pool);

        Assert.Throws<InvalidOperationException>(() => serializer.Serialize(new ProtoBufSerializerEquivalenceTests.EmptyContract()));
        Assert.Throws<NullReferenceException>(() => serializer.Serialize(null!));

        Assert.Equal(0, pool.RentCount);
    }

    private sealed class UnmappedTypeMapper : ISerializableTypeMapper
    {
        public void AddTypes(IEnumerable<Type> types)
        {
        }

        public bool TryGetId(Type type, out int id)
        {
            id = 0;
            return false;
        }

        public bool TryGetType(int id, out Type type)
        {
            type = null!;
            return false;
        }
    }
}
