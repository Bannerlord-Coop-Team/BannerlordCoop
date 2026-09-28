using System;
using System.Buffers;
using System.IO;

namespace Common.Serialization;

/// <summary>
/// Growable write-only stream over rented arrays. The rented array never leaves the stream and goes back to
/// the pool on dispose, so callers copy what they need with <see cref="CopyWrittenTo"/> first.
/// </summary>
internal sealed class PooledWriteStream : Stream
{
    private readonly ArrayPool<byte> pool;
    private byte[] buffer;
    private int length;

    public PooledWriteStream(int initialCapacity, ArrayPool<byte> pool)
    {
        this.pool = pool;
        buffer = pool.Rent(initialCapacity);
    }

    public int WrittenLength => length;

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => buffer != null;
    public override long Length => length;

    public override long Position
    {
        get => length;
        set => throw new NotSupportedException();
    }

    public void CopyWrittenTo(byte[] destination, int offset)
    {
        if (buffer == null) throw new ObjectDisposedException(nameof(PooledWriteStream));
        Buffer.BlockCopy(buffer, 0, destination, offset, length);
    }

    public override void Write(byte[] source, int offset, int count)
    {
        EnsureCapacity(count);
        Buffer.BlockCopy(source, offset, buffer, length, count);
        length += count;
    }

    public override void WriteByte(byte value)
    {
        EnsureCapacity(1);
        buffer[length++] = value;
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] destination, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        byte[] rented = buffer;
        buffer = null;
        if (rented != null) pool.Return(rented);
        base.Dispose(disposing);
    }

    private void EnsureCapacity(int count)
    {
        if (buffer == null) throw new ObjectDisposedException(nameof(PooledWriteStream));
        if (buffer.Length - length >= count) return;

        int required = checked(length + count);
        byte[] grown = pool.Rent(Math.Max(required, (int)Math.Min(int.MaxValue, buffer.Length * 2L)));
        Buffer.BlockCopy(buffer, 0, grown, 0, length);
        byte[] previous = buffer;
        buffer = grown;
        pool.Return(previous);
    }
}
