using GameInterface.Services.Locations.Messages;
using GameInterface.Surrogates;
using Missions.Locations;
using Missions.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit;

namespace E2E.Tests.Services.Locations;

public sealed class LocationAgentSpawnBatchCodecTests
{
    public LocationAgentSpawnBatchCodecTests()
    {
        _ = new SurrogateCollection();
    }

    [Fact]
    public void Encode_RoundTripsCompressedBoundedChunksInOrder()
    {
        var codec = new LocationAgentSpawnBatchCodec();
        LocationAgentSpawnData[] records = CreateRecords(100);

        IReadOnlyList<NetworkSpawnLocationAgents> encoded =
            codec.Encode(records, SpawnBatchPurpose.Initial);

        Assert.Equal(4, encoded.Count);
        Assert.All(encoded, batch =>
        {
            Assert.InRange(batch.RecordCount, 1, LocationAgentSpawnBatchCodec.MaxRecordsPerBatch);
            Assert.InRange(batch.Payload.Length, 1, LocationAgentSpawnBatchCodec.MaxWirePayloadBytes);
            Assert.Equal(SpawnBatchPurpose.Initial, batch.Purpose);
        });
        Assert.Contains(encoded, batch => batch.IsCompressed);

        LocationAgentSpawnData[] decoded = encoded
            .SelectMany(batch => DecodeWireMessage(codec, batch))
            .ToArray();
        Assert.Equal(records.Select(record => record.AgentId), decoded.Select(record => record.AgentId));
        Assert.Equal(records.Select(record => record.CharacterId), decoded.Select(record => record.CharacterId));
        Assert.Equal(records.Select(record => record.Kind), decoded.Select(record => record.Kind));
        Assert.Equal(records.Select(record => record.AuthorityRevision), decoded.Select(record => record.AuthorityRevision));
    }

    [Fact]
    public void RosterEntryAndAnimalIdentities_SurviveTheWire()
    {
        var codec = new LocationAgentSpawnBatchCodec();
        var human = new LocationAgentSpawnData(
            Guid.NewGuid(), "townswoman_empire", new Vec3(1, 2, 0), new Vec2(0, 1), 100f, "host",
            spawnEquipment: null, bodyProperties: default, clothingColor1: 0xAABBCCDD, clothingColor2: 0x11223344,
            LocationAgentKind.Human, itemId: null, harnessItemId: null, movementId: 7, movementScopeId: "host:scope",
            currentEquipment: null,
            rosterEntry: new LocationCharacterData(
                "settlement1_center", "townswoman_empire", null, null,
                "npc_common", "as_human_villager", "SandBox.AI.BehaviorSets.AddOutdoorWandererBehaviors",
                characterRelation: 0, fixedLocation: false, useCivilianEquipment: true,
                prefabBones: new[] { 22 }, prefabNames: new[] { "carry_bd_basket_a" }),
            usedPointId: 137);
        var animal = new LocationAgentSpawnData(
            Guid.NewGuid(), characterId: null, new Vec3(5, 6, 0), new Vec2(1, 0), 50f, "host",
            spawnEquipment: null, bodyProperties: default, clothingColor1: 0, clothingColor2: 0,
            LocationAgentKind.Animal, itemId: "sheep", harnessItemId: "harness_a", movementId: 8, movementScopeId: "host:scope",
            currentEquipment: null, rosterEntry: null);

        NetworkSpawnLocationAgents encoded = codec
            .Encode(new[] { human, animal }, SpawnBatchPurpose.CatchUp)
            .Single();
        LocationAgentSpawnData[] decoded = DecodeWireMessage(codec, encoded);

        var decodedHuman = decoded[0];
        Assert.Equal(LocationAgentKind.Human, decodedHuman.Kind);
        Assert.NotNull(decodedHuman.RosterEntry);
        Assert.Equal("settlement1_center", decodedHuman.RosterEntry.LocationId);
        Assert.Equal("townswoman_empire", decodedHuman.RosterEntry.CharacterId);
        Assert.Equal("npc_common", decodedHuman.RosterEntry.SpawnTag);
        Assert.Equal("SandBox.AI.BehaviorSets.AddOutdoorWandererBehaviors", decodedHuman.RosterEntry.BehaviorsMethodName);
        Assert.True(decodedHuman.RosterEntry.UseCivilianEquipment);
        Assert.Equal(new[] { 22 }, decodedHuman.RosterEntry.PrefabBones);
        Assert.Equal(new[] { "carry_bd_basket_a" }, decodedHuman.RosterEntry.PrefabNames);
        Assert.True(decodedHuman.HasUsedPoint);
        Assert.Equal(137, decodedHuman.UsedPointId);

        var decodedNoPoint = DecodeWireMessage(codec, codec.Encode(new[] { animal }, SpawnBatchPurpose.CatchUp).Single())[0];
        Assert.False(decodedNoPoint.HasUsedPoint);
        Assert.Equal(0xAABBCCDD, decodedHuman.ClothingColor1);
        Assert.Equal("host:scope", decodedHuman.MovementScopeId);
        Assert.Equal(7, decodedHuman.MovementId);

        var decodedAnimal = decoded[1];
        Assert.Equal(LocationAgentKind.Animal, decodedAnimal.Kind);
        Assert.Null(decodedAnimal.RosterEntry);
        Assert.Equal("sheep", decodedAnimal.ItemId);
        Assert.Equal("harness_a", decodedAnimal.HarnessItemId);
    }

    [Fact]
    public void TryDecode_RejectsCorruptCompressedPayload()
    {
        var codec = new LocationAgentSpawnBatchCodec();
        NetworkSpawnLocationAgents encoded = codec
            .Encode(CreateRecords(32), SpawnBatchPurpose.CatchUp)
            .Single();
        Assert.True(encoded.IsCompressed);

        byte[] corrupt = encoded.Payload.ToArray();
        corrupt[corrupt.Length / 2] ^= 0xff;
        var wire = CloneEnvelope(encoded, corrupt, encoded.RecordCount);

        Assert.False(codec.TryDecode(wire, out _));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(LocationAgentSpawnBatchCodec.MaxRecordsPerBatch + 1)]
    public void TryDecode_RejectsRecordCountsOutsideBatchBounds(int recordCount)
    {
        var codec = new LocationAgentSpawnBatchCodec();
        NetworkSpawnLocationAgents encoded = codec
            .Encode(CreateRecords(1), SpawnBatchPurpose.Initial)
            .Single();
        var wire = CloneEnvelope(encoded, encoded.Payload, recordCount);

        Assert.False(codec.TryDecode(wire, out _));
    }

    private static NetworkSpawnLocationAgents CloneEnvelope(
        NetworkSpawnLocationAgents encoded, byte[] payload, int recordCount)
    {
        return new NetworkSpawnLocationAgents(
            payload,
            encoded.UncompressedLength,
            recordCount,
            encoded.IsCompressed,
            encoded.TransferId,
            encoded.BatchIndex,
            encoded.BatchCount,
            encoded.Purpose,
            encoded.PayloadSha256,
            agents: null!);
    }

    private static LocationAgentSpawnData[] DecodeWireMessage(
        LocationAgentSpawnBatchCodec codec,
        NetworkSpawnLocationAgents encoded)
    {
        var wire = CloneEnvelope(encoded, encoded.Payload, encoded.RecordCount);
        Assert.True(codec.TryDecode(wire, out LocationAgentSpawnData[] decoded));
        return decoded;
    }

    private static LocationAgentSpawnData[] CreateRecords(int count)
    {
        var records = new LocationAgentSpawnData[count];
        for (int i = 0; i < count; i++)
        {
            records[i] = new LocationAgentSpawnData(
                Guid.NewGuid(),
                "townsman_empire",
                new Vec3(i, i + 1, 0f),
                new Vec2(0, 1),
                100f,
                "host",
                spawnEquipment: null,
                bodyProperties: default,
                clothingColor1: (uint)i,
                clothingColor2: (uint)(i * 2),
                LocationAgentKind.Human,
                itemId: null,
                harnessItemId: null,
                movementId: (ushort)(i + 1),
                movementScopeId: "host:scope",
                currentEquipment: null,
                rosterEntry: new LocationCharacterData(
                    "settlement1_center", "townsman_empire", null, null,
                    "npc_common", null, null, 0, false, true),
                authorityRevision: i + 4);
        }
        return records;
    }
}
