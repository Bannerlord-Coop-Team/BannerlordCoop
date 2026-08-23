using K4os.Compression.LZ4;
using Common.Util;
using Missions.Messages;
using ProtoBuf;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace Missions.Battles;

public interface IBattleAgentSpawnBatchCodec
{
    IReadOnlyList<NetworkSpawnBattleAgents> Encode(
        IReadOnlyList<BattleAgentSpawnData> agents,
        SpawnBatchPurpose purpose);

    bool TryDecode(
        NetworkSpawnBattleAgents message,
        out BattleAgentSpawnData[] agents);
}

/// <summary>Creates bounded LZ4 spawn batches and rejects malformed payloads before they reach the game thread.</summary>
public sealed class BattleAgentSpawnBatchCodec : IBattleAgentSpawnBatchCodec
{
    internal const int MaxRecordsPerBatch = 32;
    internal const int MaxUncompressedBytes = 256 * 1024;
    internal const int MaxWirePayloadBytes = 256 * 1024;

    public IReadOnlyList<NetworkSpawnBattleAgents> Encode(
        IReadOnlyList<BattleAgentSpawnData> agents,
        SpawnBatchPurpose purpose)
    {
        if (agents == null) throw new ArgumentNullException(nameof(agents));
        if (agents.Count == 0) return Array.Empty<NetworkSpawnBattleAgents>();

        var drafts = new List<EncodedBatch>();
        for (int start = 0; start < agents.Count; start += MaxRecordsPerBatch)
        {
            int count = Math.Min(MaxRecordsPerBatch, agents.Count - start);
            EncodeBounded(agents, start, count, drafts);
        }

        var transferId = Guid.NewGuid();
        var result = new NetworkSpawnBattleAgents[drafts.Count];
        for (int i = 0; i < drafts.Count; i++)
        {
            EncodedBatch draft = drafts[i];
            result[i] = new NetworkSpawnBattleAgents(
                draft.Payload,
                draft.UncompressedLength,
                draft.Agents.Length,
                draft.IsCompressed,
                transferId,
                i,
                drafts.Count,
                purpose,
                draft.PayloadSha256,
                draft.Agents);
        }
        return result;
    }

    public bool TryDecode(
        NetworkSpawnBattleAgents message,
        out BattleAgentSpawnData[] agents)
    {
        agents = null;
        if (message == null) return false;

        // Directly published messages are used by the mission harness. Production messages always carry Payload.
        if (message.Agents != null)
        {
            agents = message.Agents;
            return true;
        }

        if (message.Payload == null ||
            message.Payload.Length == 0 ||
            message.Payload.Length > MaxWirePayloadBytes ||
            message.UncompressedLength <= 0 ||
            message.UncompressedLength > MaxUncompressedBytes ||
            message.RecordCount <= 0 ||
            message.RecordCount > MaxRecordsPerBatch ||
            message.PayloadSha256 == null ||
            message.PayloadSha256.Length != 32 ||
            message.BatchCount <= 0 ||
            message.BatchIndex < 0 ||
            message.BatchIndex >= message.BatchCount)
        {
            return false;
        }

        byte[] serialized;
        if (message.IsCompressed)
        {
            serialized = new byte[message.UncompressedLength];
            try
            {
                int decoded = LZ4Codec.Decode(
                    message.Payload, 0, message.Payload.Length,
                    serialized, 0, serialized.Length);
                if (decoded != serialized.Length) return false;
            }
            catch (Exception)
            {
                return false;
            }
        }
        else
        {
            if (message.Payload.Length != message.UncompressedLength) return false;
            serialized = message.Payload;
        }

        try
        {
            if (!HashesEqual(message.PayloadSha256, ComputeHash(serialized)))
                return false;

            using var stream = new MemoryStream(serialized, writable: false);
            SpawnBatchPayload batch;
            using (new AllowedThread())
            {
                batch = Serializer.Deserialize<SpawnBatchPayload>(stream);
            }
            if (batch?.Agents == null || batch.Agents.Length != message.RecordCount)
                return false;

            agents = batch.Agents;
            return true;
        }
        catch (Exception)
        {
            agents = null;
            return false;
        }
    }

    private static void EncodeBounded(
        IReadOnlyList<BattleAgentSpawnData> agents,
        int start,
        int count,
        List<EncodedBatch> output)
    {
        var records = new BattleAgentSpawnData[count];
        for (int i = 0; i < count; i++)
            records[i] = agents[start + i];

        byte[] serialized = Serialize(records);
        if (serialized.Length > MaxUncompressedBytes)
        {
            if (count == 1)
                throw new InvalidOperationException(
                    $"Spawn record {records[0]?.AgentId} exceeds the {MaxUncompressedBytes}-byte batch limit.");

            int firstCount = count / 2;
            EncodeBounded(agents, start, firstCount, output);
            EncodeBounded(agents, start + firstCount, count - firstCount, output);
            return;
        }

        output.Add(Compress(records, serialized));
    }

    private static byte[] Serialize(BattleAgentSpawnData[] agents)
    {
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, new SpawnBatchPayload(agents));
        return stream.ToArray();
    }

    private static EncodedBatch Compress(
        BattleAgentSpawnData[] agents,
        byte[] serialized)
    {
        int maximumLength = LZ4Codec.MaximumOutputSize(serialized.Length);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(maximumLength);
        try
        {
            int compressedLength = LZ4Codec.Encode(
                serialized, 0, serialized.Length,
                buffer, 0, maximumLength,
                LZ4Level.L00_FAST);
            if (compressedLength > 0 &&
                compressedLength < serialized.Length &&
                compressedLength <= MaxWirePayloadBytes)
            {
                var compressed = new byte[compressedLength];
                Buffer.BlockCopy(buffer, 0, compressed, 0, compressedLength);
                return new EncodedBatch(
                    agents,
                    compressed,
                    serialized.Length,
                    true,
                    ComputeHash(serialized));
            }

            if (serialized.Length > MaxWirePayloadBytes)
                throw new InvalidOperationException(
                    $"Spawn batch could not fit the {MaxWirePayloadBytes}-byte wire limit.");

            return new EncodedBatch(
                agents,
                serialized,
                serialized.Length,
                false,
                ComputeHash(serialized));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static byte[] ComputeHash(byte[] payload)
    {
        using var sha256 = SHA256.Create();
        return sha256.ComputeHash(payload);
    }

    private static bool HashesEqual(byte[] left, byte[] right)
    {
        if (left.Length != right.Length) return false;

        int difference = 0;
        for (int i = 0; i < left.Length; i++)
            difference |= left[i] ^ right[i];
        return difference == 0;
    }

    [ProtoContract(SkipConstructor = true)]
    private sealed class SpawnBatchPayload
    {
        [ProtoMember(1)]
        public readonly BattleAgentSpawnData[] Agents;

        public SpawnBatchPayload(BattleAgentSpawnData[] agents)
        {
            Agents = agents;
        }
    }

    private sealed class EncodedBatch
    {
        public BattleAgentSpawnData[] Agents { get; }
        public byte[] Payload { get; }
        public int UncompressedLength { get; }
        public bool IsCompressed { get; }
        public byte[] PayloadSha256 { get; }

        public EncodedBatch(
            BattleAgentSpawnData[] agents,
            byte[] payload,
            int uncompressedLength,
            bool isCompressed,
            byte[] payloadSha256)
        {
            Agents = agents;
            Payload = payload;
            UncompressedLength = uncompressedLength;
            IsCompressed = isCompressed;
            PayloadSha256 = payloadSha256;
        }
    }
}
