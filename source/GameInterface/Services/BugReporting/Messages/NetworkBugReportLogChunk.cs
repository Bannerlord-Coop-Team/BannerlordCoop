using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.BugReporting.Messages;

/// <summary>
/// One bounded chunk of a gzip-compressed client co-op log.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkBugReportLogChunk : ICommand
{
    public const int ChunkSize = 4 * 1024;

    [ProtoMember(1)]
    public string RequestId { get; }

    [ProtoMember(2)]
    public int ChunkIndex { get; }

    [ProtoMember(3)]
    public int ChunkCount { get; }

    [ProtoMember(4)]
    public int CompressedLength { get; }

    [ProtoMember(5)]
    public int UncompressedLength { get; }

    [ProtoMember(6)]
    public byte[] Data { get; }

    public NetworkBugReportLogChunk(
        string requestId,
        int chunkIndex,
        int chunkCount,
        int compressedLength,
        int uncompressedLength,
        byte[] data)
    {
        RequestId = requestId;
        ChunkIndex = chunkIndex;
        ChunkCount = chunkCount;
        CompressedLength = compressedLength;
        UncompressedLength = uncompressedLength;
        Data = data;
    }
}
