using Missions.Battles;
using Missions.Messages;
using GameInterface.Surrogates;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit;

namespace E2E.Tests.Services.Missions;

public sealed class BattleAgentSpawnBatchCodecTests
{
    public BattleAgentSpawnBatchCodecTests()
    {
        _ = new SurrogateCollection();
    }

    [Fact]
    public void Encode_RoundTripsCompressedBoundedChunksInOrder()
    {
        var codec = new BattleAgentSpawnBatchCodec();
        BattleAgentSpawnData[] records = CreateRecords(100);

        IReadOnlyList<NetworkSpawnBattleAgents> encoded =
            codec.Encode(records, SpawnBatchPurpose.Deployment);

        Assert.Equal(4, encoded.Count);
        Assert.All(encoded, batch =>
        {
            Assert.InRange(batch.RecordCount, 1, BattleAgentSpawnBatchCodec.MaxRecordsPerBatch);
            Assert.InRange(batch.Payload.Length, 1, BattleAgentSpawnBatchCodec.MaxWirePayloadBytes);
            Assert.Equal(SpawnBatchPurpose.Deployment, batch.Purpose);
        });
        Assert.Contains(encoded, batch => batch.IsCompressed);

        BattleAgentSpawnData[] decoded = encoded
            .SelectMany(batch => DecodeWireMessage(codec, batch))
            .ToArray();
        Assert.Equal(records.Select(record => record.AgentId), decoded.Select(record => record.AgentId));
        Assert.Equal(records.Select(record => record.CharacterId), decoded.Select(record => record.CharacterId));
    }

    [Fact]
    public void EncodedBatch_RoundTripsOuterMessageThroughProtobuf()
    {
        var codec = new BattleAgentSpawnBatchCodec();
        NetworkSpawnBattleAgents encoded = codec
            .Encode(CreateRecords(32), SpawnBatchPurpose.CatchUp)
            .Single();

        NetworkSpawnBattleAgents wire = ProtoBuf.Serializer.DeepClone(encoded);

        Assert.Equal(encoded.Payload, wire.Payload);
        Assert.Equal(encoded.UncompressedLength, wire.UncompressedLength);
        Assert.Equal(encoded.RecordCount, wire.RecordCount);
        Assert.Equal(encoded.IsCompressed, wire.IsCompressed);
        Assert.Equal(encoded.TransferId, wire.TransferId);
        Assert.Equal(encoded.BatchIndex, wire.BatchIndex);
        Assert.Equal(encoded.BatchCount, wire.BatchCount);
        Assert.Equal(encoded.Purpose, wire.Purpose);
        Assert.Equal(encoded.PayloadSha256, wire.PayloadSha256);
        Assert.True(codec.TryDecode(wire, out BattleAgentSpawnData[] decoded));
        Assert.Equal(encoded.RecordCount, decoded.Length);
    }

    [Fact]
    public void TryDecode_RejectsCorruptCompressedPayload()
    {
        var codec = new BattleAgentSpawnBatchCodec();
        NetworkSpawnBattleAgents encoded = codec
            .Encode(CreateRecords(32), SpawnBatchPurpose.CatchUp)
            .Single();
        Assert.True(encoded.IsCompressed);

        byte[] corrupt = encoded.Payload.ToArray();
        corrupt[corrupt.Length / 2] ^= 0xff;
        var wire = new NetworkSpawnBattleAgents(
            corrupt,
            encoded.UncompressedLength,
            encoded.RecordCount,
            encoded.IsCompressed,
            encoded.TransferId,
            encoded.BatchIndex,
            encoded.BatchCount,
            encoded.Purpose,
            encoded.PayloadSha256,
            agents: null!);

        Assert.False(codec.TryDecode(wire, out _));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(BattleAgentSpawnBatchCodec.MaxRecordsPerBatch + 1)]
    public void TryDecode_RejectsRecordCountsOutsideBatchBounds(int recordCount)
    {
        var codec = new BattleAgentSpawnBatchCodec();
        NetworkSpawnBattleAgents encoded = codec
            .Encode(CreateRecords(1), SpawnBatchPurpose.Initial)
            .Single();
        var wire = new NetworkSpawnBattleAgents(
            encoded.Payload,
            encoded.UncompressedLength,
            recordCount,
            encoded.IsCompressed,
            encoded.TransferId,
            encoded.BatchIndex,
            encoded.BatchCount,
            encoded.Purpose,
            encoded.PayloadSha256,
            agents: null!);

        Assert.False(codec.TryDecode(wire, out _));
    }

    private static BattleAgentSpawnData[] DecodeWireMessage(
        BattleAgentSpawnBatchCodec codec,
        NetworkSpawnBattleAgents encoded)
    {
        var wire = new NetworkSpawnBattleAgents(
            encoded.Payload,
            encoded.UncompressedLength,
            encoded.RecordCount,
            encoded.IsCompressed,
            encoded.TransferId,
            encoded.BatchIndex,
            encoded.BatchCount,
            encoded.Purpose,
            encoded.PayloadSha256,
            agents: null!);
        Assert.True(codec.TryDecode(wire, out BattleAgentSpawnData[] decoded));
        return decoded;
    }

    private static BattleAgentSpawnData[] CreateRecords(int count)
    {
        var records = new BattleAgentSpawnData[count];
        for (int i = 0; i < count; i++)
        {
            records[i] = new BattleAgentSpawnData(
                Guid.NewGuid(),
                "imperial_infantry",
                new Vec3(i, i + 1, 0f),
                BattleSideEnum.Attacker,
                100f,
                "owner",
                "map_event_party",
                i,
                default(Equipment),
                default(BodyProperties),
                missionEquipmentData: null,
                movementId: (ushort)(i + 1),
                movementScopeId: "owner:scope");
        }
        return records;
    }
}
