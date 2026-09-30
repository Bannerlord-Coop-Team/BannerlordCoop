using Common.Serialization;
using System.Buffers;

namespace Common.Tests.Serialization;

/// <summary>Growth, written length and pool ownership of <see cref="PooledWriteStream"/>.</summary>
public class PooledWriteStreamTests
{
    [Fact]
    public void Write_GrowsPastInitialCapacityAndKeepsEveryByte()
    {
        var pool = new TrackingArrayPool();
        byte[] expected = Enumerable.Range(0, 1000).Select(i => (byte)(i * 7)).ToArray();

        using (var stream = new PooledWriteStream(16, pool))
        {
            stream.WriteByte(expected[0]);
            stream.Write(expected, 1, 99);
            stream.Write(expected, 100, 900);

            var actual = new byte[stream.WrittenLength];
            stream.CopyWrittenTo(actual, 0);
            Assert.Equal(expected, actual);
            Assert.Equal(1000, stream.Length);
            Assert.Equal(1000, stream.Position);
        }

        Assert.True(pool.RentCount > 1);
    }

    [Fact]
    public void WrittenLength_CountsWrittenBytesNotRentedCapacity()
    {
        var pool = new TrackingArrayPool();
        using var stream = new PooledWriteStream(64, pool);

        stream.Write(new byte[] { 1, 2, 3, 4, 5 }, 1, 3);

        Assert.Equal(3, stream.WrittenLength);
        Assert.True(pool.Outstanding.Single().Length > 64);
        var destination = new byte[] { 9, 0, 0, 0, 9 };
        stream.CopyWrittenTo(destination, 1);
        Assert.Equal(new byte[] { 9, 2, 3, 4, 9 }, destination);
    }

    [Fact]
    public void Dispose_ReturnsEveryRentedArrayExactlyOnce()
    {
        var pool = new TrackingArrayPool();
        var stream = new PooledWriteStream(8, pool);
        for (int i = 0; i < 50; i++) stream.Write(new byte[100], 0, 100);

        stream.Dispose();
        stream.Dispose();

        Assert.True(pool.RentCount >= 3);
        Assert.Equal(pool.RentCount, pool.ReturnCount);
        Assert.Empty(pool.Outstanding);
    }

    [Fact]
    public void Write_AfterDisposeThrows()
    {
        var stream = new PooledWriteStream(8, new TrackingArrayPool());
        stream.WriteByte(1);
        stream.Dispose();

        Assert.False(stream.CanWrite);
        Assert.Throws<ObjectDisposedException>(() => stream.WriteByte(2));
        Assert.Throws<ObjectDisposedException>(() => stream.Write(new byte[] { 3 }, 0, 1));
        Assert.Throws<ObjectDisposedException>(() => stream.CopyWrittenTo(new byte[1], 0));
    }

    [Fact]
    public void Write_InvalidArgumentsThrowAndKeepWrittenLength()
    {
        using var stream = new PooledWriteStream(8, new TrackingArrayPool());
        stream.Write(new byte[] { 1, 2 }, 0, 2);

        Assert.Throws<ArgumentNullException>(() => stream.Write(null!, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.Write(new byte[4], -1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.Write(new byte[4], 0, -1));
        Assert.Throws<ArgumentException>(() => stream.Write(new byte[4], 3, 2));
        Assert.Equal(2, stream.WrittenLength);
    }

    [Fact]
    public void ReadAndSeek_AreNotSupported()
    {
        using var stream = new PooledWriteStream(8, new TrackingArrayPool());

        Assert.False(stream.CanRead);
        Assert.False(stream.CanSeek);
        Assert.Throws<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
        Assert.Throws<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.Throws<NotSupportedException>(() => stream.SetLength(0));
        Assert.Throws<NotSupportedException>(() => stream.Position = 0);
    }
}

/// <summary>
/// Hands out arrays larger than requested, fails a double return and overwrites returned arrays,
/// so a read after return or a leaked pool array shows up in the bytes.
/// </summary>
internal sealed class TrackingArrayPool : ArrayPool<byte>
{
    private readonly bool reuse;
    private readonly Stack<byte[]> free = new Stack<byte[]>();
    private readonly List<byte[]> everRented = new List<byte[]>();

    public TrackingArrayPool(bool reuse = false)
    {
        this.reuse = reuse;
    }

    public HashSet<byte[]> Outstanding { get; } = new HashSet<byte[]>(ReferenceEqualityComparer.Instance);
    public int RentCount { get; private set; }
    public int ReturnCount { get; private set; }

    public bool EverRented(byte[] array) => everRented.Any(rented => ReferenceEquals(rented, array));

    public override byte[] Rent(int minimumLength)
    {
        byte[] array = reuse && free.Count > 0 && free.Peek().Length >= minimumLength
            ? free.Pop()
            : new byte[minimumLength + 13];
        if (!Outstanding.Add(array)) throw new InvalidOperationException("Array rented twice without a return.");
        everRented.Add(array);
        RentCount++;
        return array;
    }

    public override void Return(byte[] array, bool clearArray = false)
    {
        if (!Outstanding.Remove(array)) throw new InvalidOperationException("Array returned twice or never rented.");
        ReturnCount++;
        array.AsSpan().Fill(0xCD);
        if (reuse) free.Push(array);
    }
}
