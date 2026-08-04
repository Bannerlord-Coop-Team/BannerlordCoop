using Common.Messaging;
using GameInterface.Services.Locations.Messages;
using Missions.Agents.Packets;
using ProtoBuf;
using System;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Missions.Messages;

/// <summary>
/// Location NPC host to peers over the mission mesh: one bounded, optionally compressed batch of
/// settlement agents for peers to recreate as puppets (SR-020..SR-025). Envelope mirrors
/// <see cref="NetworkSpawnBattleAgents"/>; the codec owns the wire representation and validates it
/// before any game state is read.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkSpawnLocationAgents : IEvent
{
    [ProtoMember(1)]
    public readonly byte[] Payload = Array.Empty<byte>();
    [ProtoMember(2)]
    public readonly int UncompressedLength;
    [ProtoMember(3)]
    public readonly int RecordCount;
    [ProtoMember(4)]
    public readonly bool IsCompressed;
    [ProtoMember(5)]
    public readonly Guid TransferId;
    [ProtoMember(6)]
    public readonly int BatchIndex;
    [ProtoMember(7)]
    public readonly int BatchCount;
    [ProtoMember(8)]
    public readonly SpawnBatchPurpose Purpose;
    [ProtoMember(9)]
    public readonly byte[] PayloadSha256 = Array.Empty<byte>();

    // Kept only on an in-process message so tests and the sending side can inspect exact records. Protobuf
    // carries Payload instead, so no uncompressed duplicate crosses the network.
    [ProtoIgnore]
    public readonly LocationAgentSpawnData[] Agents;

    public NetworkSpawnLocationAgents(LocationAgentSpawnData[] agents)
    {
        Agents = agents ?? Array.Empty<LocationAgentSpawnData>();
        RecordCount = Agents.Length;
        BatchCount = 1;
    }

    internal NetworkSpawnLocationAgents(
        byte[] payload,
        int uncompressedLength,
        int recordCount,
        bool isCompressed,
        Guid transferId,
        int batchIndex,
        int batchCount,
        SpawnBatchPurpose purpose,
        byte[] payloadSha256,
        LocationAgentSpawnData[] agents)
    {
        Payload = payload;
        UncompressedLength = uncompressedLength;
        RecordCount = recordCount;
        IsCompressed = isCompressed;
        TransferId = transferId;
        BatchIndex = batchIndex;
        BatchCount = batchCount;
        Purpose = purpose;
        PayloadSha256 = payloadSha256;
        Agents = agents;
    }
}

public enum LocationAgentKind : byte
{
    Human = 0,
    Animal = 1,
}

/// <summary>
/// One host-spawned settlement agent: its network id, who it is, where it spawned, its visuals as the
/// host rolled them, and — for humans — its roster identity (SR-022) so the receiver binds the puppet
/// to a LOCAL <c>LocationCharacter</c> entry and builds it from that entry's origin. Settlement humans
/// always spawn on foot (native passes NoHorses everywhere), so there is no mount linkage; animals
/// (incl. scene horses) are standalone <see cref="LocationAgentKind.Animal"/> records carrying their
/// item identities instead.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class LocationAgentSpawnData
{
    [ProtoMember(1)]
    public readonly Guid AgentId;
    [ProtoMember(2)]
    public readonly string CharacterId;
    [ProtoMember(3)]
    public readonly Vec3 Position;
    [ProtoMember(4)]
    public readonly Vec2 Direction;
    [ProtoMember(5)]
    public readonly float Health;
    [ProtoMember(6)]
    public readonly string OwnerControllerId;
    [ProtoMember(7)]
    public readonly string OriginalOwnerControllerId;
    [ProtoMember(8)]
    public readonly Equipment SpawnEquipment;
    [ProtoMember(9)]
    public readonly BodyProperties BodyProperties;
    [ProtoMember(10)]
    public readonly uint ClothingColor1;
    [ProtoMember(11)]
    public readonly uint ClothingColor2;
    [ProtoMember(12)]
    public readonly LocationAgentKind Kind;
    // Animal records: the ObjectManager ids of the monster's item (e.g. "sheep", a horse item) and
    // its harness, consumed by Mission.SpawnMonster on the receiver.
    [ProtoMember(13)]
    public readonly string ItemId;
    [ProtoMember(14)]
    public readonly string HarnessItemId;
    // Compact identity used only by the high-frequency movement stream. The canonical Guid remains
    // authoritative for reliable gameplay messages.
    [ProtoMember(15)]
    public readonly ushort MovementId;
    [ProtoMember(16)]
    public readonly string MovementScopeId;
    [ProtoMember(17)]
    public readonly AgentEquipmentData CurrentEquipment;
    [ProtoMember(18)]
    public readonly bool HasCurrentEquipment;
    // Roster identity (humans, SR-022): the LocationCharacter entry this agent spawned from, so the
    // receiver binds an existing local entry (server-synced heroes) or reconstructs one (ambient).
    // Null for animals and for the rare human spawn with no roster entry.
    [ProtoMember(19)]
    public readonly LocationCharacterData RosterEntry;

    public LocationAgentSpawnData(
        Guid agentId,
        string characterId,
        Vec3 position,
        Vec2 direction,
        float health,
        string ownerControllerId,
        Equipment spawnEquipment,
        BodyProperties bodyProperties,
        uint clothingColor1,
        uint clothingColor2,
        LocationAgentKind kind,
        string itemId,
        string harnessItemId,
        ushort movementId,
        string movementScopeId,
        AgentEquipmentData? currentEquipment,
        LocationCharacterData rosterEntry,
        string originalOwnerControllerId = null)
    {
        AgentId = agentId;
        CharacterId = characterId;
        Position = position;
        Direction = direction;
        Health = health;
        OwnerControllerId = ownerControllerId;
        OriginalOwnerControllerId = originalOwnerControllerId ?? ownerControllerId;
        SpawnEquipment = spawnEquipment;
        BodyProperties = bodyProperties;
        ClothingColor1 = clothingColor1;
        ClothingColor2 = clothingColor2;
        Kind = kind;
        ItemId = itemId;
        HarnessItemId = harnessItemId;
        MovementId = movementId;
        MovementScopeId = movementScopeId ?? OriginalOwnerControllerId;
        CurrentEquipment = currentEquipment.GetValueOrDefault();
        HasCurrentEquipment = currentEquipment.HasValue;
        RosterEntry = rosterEntry;
    }
}
