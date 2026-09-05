using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CoopMcpServer;

public interface IIncrementalLogReader
{
    LogChunk Read(string path, string cursor, int maxBytes);
}

public sealed record LogChunk(string Text, string Cursor, bool Reset, bool HasMore, long Length);

public sealed class IncrementalLogReader : IIncrementalLogReader
{
    private const int ProbeLimit = (1024 * 1024) + 512;
    private sealed record CursorState(string Path, long CreatedTicks, long Offset, int ProbeLength,
        string ProbeHash, string BoundaryHash);

    public LogChunk Read(string path, string cursor, int maxBytes)
    {
        if (maxBytes < 4 || maxBytes > 65536) throw new ArgumentOutOfRangeException(nameof(maxBytes), "max_bytes must be 4..65536.");
        CursorState previous = null;
        if (cursor != null)
        {
            if (cursor.Length > 8192) throw new ArgumentException("Invalid log cursor.");
            try { previous = JsonSerializer.Deserialize<CursorState>(Convert.FromBase64String(cursor)); }
            catch (Exception e) when (e is FormatException || e is JsonException)
            { throw new ArgumentException("Invalid log cursor.", e); }
            if (previous == null || previous.Offset < 0 || previous.ProbeLength < 0 || previous.ProbeLength > ProbeLimit)
                throw new ArgumentException("Invalid log cursor.");
        }
        path = System.IO.Path.GetFullPath(path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        long created = File.GetCreationTimeUtc(path).Ticks;
        long length = stream.Length;
        bool reset = previous != null && (previous.Path != path || previous.CreatedTicks != created ||
            previous.Offset > length || previous.ProbeLength > length ||
            previous.ProbeHash != Hash(stream, 0, previous.ProbeLength) ||
            previous.BoundaryHash != Hash(stream, Math.Max(0, previous.Offset - 256), (int)Math.Min(256, previous.Offset)));
        long offset = previous == null || reset ? 0 : previous.Offset;
        int probeLength = (int)Math.Min(length, ProbeLimit);
        string probeHash = Hash(stream, 0, probeLength);
        stream.Position = offset;
        var buffer = new byte[(int)Math.Min(maxBytes, length - offset)];
        int count = stream.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false);
        bool reachedEnd = offset + count >= length;
        // Leave incomplete trailing UTF-8 characters for the next read.
        if (count > 0)
        {
            int start = count - 1;
            while (start > 0 && (buffer[start] & 0xC0) == 0x80) start--;
            int expected = buffer[start] >= 0xF0 ? 4 : buffer[start] >= 0xE0 ? 3 : buffer[start] >= 0xC0 ? 2 : 1;
            if (count - start < expected) count = start;
        }
        offset += count;
        string boundaryHash = Hash(stream, Math.Max(0, offset - 256), (int)Math.Min(256, offset));
        if (stream.Length < length || probeHash != Hash(stream, 0, probeLength) || File.GetCreationTimeUtc(path).Ticks != created)
            throw new IOException("Log changed while reading; retry read_logs with the same cursor.");
        var next = new CursorState(path, created, offset, probeLength, probeHash, boundaryHash);
        return new LogChunk(Encoding.UTF8.GetString(buffer, 0, count),
            Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(next)), reset, !reachedEnd && offset < length, length);
    }

    private string Hash(Stream stream, long offset, int count)
    {
        stream.Position = offset;
        var bytes = new byte[count];
        int read = stream.ReadAtLeast(bytes, count, throwOnEndOfStream: false);
        return Convert.ToHexString(SHA256.HashData(bytes.AsSpan(0, read)));
    }
}
