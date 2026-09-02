namespace VerificationHarness.DedicatedServerSynthetic;

public interface IDedicatedServerSaveTransferCollector
{
    int? TransferId { get; }
    int ReceivedChunkCount { get; }
    int ReceivedCompressedBytes { get; }
    bool IsComplete { get; }
    void Add(DedicatedSaveChunk chunk);
    byte[] AssembleCompressed();
}

public sealed class DedicatedServerSaveTransferCollector : IDedicatedServerSaveTransferCollector
{
    public const int MaximumChunkBytes = 64 * 1024;
    public const int MaximumChunkCount = 4096;
    public const int MaximumCompressedBytes = 64 * 1024 * 1024;
    public const int MaximumUncompressedBytes = 256 * 1024 * 1024;

    private readonly SortedDictionary<int, byte[]> chunks = new();
    private int? chunkCount;
    private int? compressedSize;
    private int? uncompressedSize;

    public int? TransferId { get; private set; }
    public int ReceivedChunkCount => chunks.Count;
    public int ReceivedCompressedBytes { get; private set; }
    public bool IsComplete =>
        TransferId.HasValue &&
        chunkCount.HasValue &&
        chunks.Count == chunkCount.Value &&
        ReceivedCompressedBytes == compressedSize;

    public void Add(DedicatedSaveChunk chunk)
    {
        if (chunk == null) throw new ArgumentNullException(nameof(chunk));
        ValidateChunk(chunk);

        if (!TransferId.HasValue)
        {
            TransferId = chunk.TransferId;
            chunkCount = chunk.ChunkCount;
            compressedSize = chunk.CompressedSize;
            uncompressedSize = chunk.UncompressedSize;
        }
        else if (TransferId != chunk.TransferId ||
                 chunkCount != chunk.ChunkCount ||
                 compressedSize != chunk.CompressedSize ||
                 uncompressedSize != chunk.UncompressedSize)
        {
            throw new InvalidDataException("Save chunk metadata changed within one transfer.");
        }

        if (chunks.ContainsKey(chunk.ChunkIndex))
        {
            throw new InvalidDataException($"Save chunk {chunk.ChunkIndex} was received more than once.");
        }

        if (ReceivedCompressedBytes + chunk.ChunkData.Length > compressedSize)
        {
            throw new InvalidDataException("Save chunks exceed the declared compressed size.");
        }

        chunks.Add(chunk.ChunkIndex, chunk.ChunkData.ToArray());
        ReceivedCompressedBytes += chunk.ChunkData.Length;
    }

    public byte[] AssembleCompressed()
    {
        if (!IsComplete)
        {
            throw new InvalidOperationException("The bounded save transfer is not complete.");
        }

        var result = new byte[compressedSize!.Value];
        int offset = 0;
        foreach ((_, byte[] data) in chunks)
        {
            Buffer.BlockCopy(data, 0, result, offset, data.Length);
            offset += data.Length;
        }

        for (int index = 0; index < chunkCount; index++)
        {
            if (!chunks.ContainsKey(index))
            {
                throw new InvalidOperationException($"Save transfer is missing chunk {index}.");
            }
        }

        if (offset != result.Length)
        {
            throw new InvalidOperationException(
                $"Save transfer assembled {offset} bytes, expected {result.Length}.");
        }

        return result;
    }

    private static void ValidateChunk(DedicatedSaveChunk chunk)
    {
        if (chunk.TransferId <= 0)
        {
            throw new InvalidDataException("Save transfer id must be positive.");
        }

        if (chunk.ChunkCount is <= 0 or > MaximumChunkCount)
        {
            throw new InvalidDataException(
                $"Save transfer chunk count must be between 1 and {MaximumChunkCount}.");
        }

        if (chunk.ChunkIndex < 0 || chunk.ChunkIndex >= chunk.ChunkCount)
        {
            throw new InvalidDataException("Save chunk index is outside the declared transfer.");
        }

        if (chunk.CompressedSize is <= 0 or > MaximumCompressedBytes)
        {
            throw new InvalidDataException(
                $"Compressed save size must be between 1 and {MaximumCompressedBytes} bytes.");
        }

        if (chunk.UncompressedSize is <= 0 or > MaximumUncompressedBytes)
        {
            throw new InvalidDataException(
                $"Uncompressed save size must be between 1 and {MaximumUncompressedBytes} bytes.");
        }

        if (chunk.ChunkData == null || chunk.ChunkData.Length == 0)
        {
            throw new InvalidDataException("Save chunk data is empty.");
        }

        if (chunk.ChunkData.Length > MaximumChunkBytes)
        {
            throw new InvalidDataException(
                $"Save chunks cannot exceed {MaximumChunkBytes} bytes.");
        }
    }
}
