using VerificationHarness.DedicatedServerSynthetic;

namespace VerificationHarness.Tests.DedicatedServerSynthetic;

public sealed class DedicatedServerSaveTransferCollectorTests
{
    [Fact]
    public void CompleteTransfer_AssemblesInChunkOrder()
    {
        var collector = new DedicatedServerSaveTransferCollector();
        collector.Add(Chunk(1, 2, new byte[] { 3, 4 }, compressedSize: 4));
        collector.Add(Chunk(0, 2, new byte[] { 1, 2 }, compressedSize: 4));

        Assert.True(collector.IsComplete);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, collector.AssembleCompressed());
    }

    [Fact]
    public void DuplicateChunk_IsRejected()
    {
        var collector = new DedicatedServerSaveTransferCollector();
        collector.Add(Chunk(0, 1, new byte[] { 1 }, compressedSize: 1));

        Assert.Throws<InvalidDataException>(() =>
            collector.Add(Chunk(0, 1, new byte[] { 1 }, compressedSize: 1)));
    }

    [Theory]
    [InlineData(DedicatedServerSaveTransferCollector.MaximumChunkCount + 1, 1, 1)]
    [InlineData(1, DedicatedServerSaveTransferCollector.MaximumCompressedBytes + 1, 1)]
    [InlineData(1, 1, DedicatedServerSaveTransferCollector.MaximumUncompressedBytes + 1)]
    public void DeclaredBounds_AreEnforced(int chunkCount, int compressedSize, int uncompressedSize)
    {
        var collector = new DedicatedServerSaveTransferCollector();
        var chunk = new DedicatedSaveChunk(
            1,
            0,
            chunkCount,
            compressedSize,
            uncompressedSize,
            new byte[] { 1 });

        Assert.Throws<InvalidDataException>(() => collector.Add(chunk));
    }

    [Fact]
    public void ChunkDataLimit_IsEnforced()
    {
        var collector = new DedicatedServerSaveTransferCollector();
        var data = new byte[DedicatedServerSaveTransferCollector.MaximumChunkBytes + 1];
        var chunk = new DedicatedSaveChunk(1, 0, 1, data.Length, data.Length, data);

        Assert.Throws<InvalidDataException>(() => collector.Add(chunk));
    }

    private static DedicatedSaveChunk Chunk(
        int index,
        int count,
        byte[] data,
        int compressedSize)
    {
        return new DedicatedSaveChunk(7, index, count, compressedSize, 16, data);
    }
}
