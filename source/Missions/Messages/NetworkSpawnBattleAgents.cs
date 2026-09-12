using Common.Messaging;
using Missions.Agents.Packets;
using Missions.Data;
using ProtoBuf;
using System;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Messages;

/// <summary>
/// Owner to peers over the mission mesh: one bounded, optionally compressed batch of agents for peers to
/// recreate as puppets. The codec owns the wire representation and validates it before any game state is read.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkSpawnBattleAgents : IEvent
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
    public readonly BattleAgentSpawnData[] Agents;

    public NetworkSpawnBattleAgents(BattleAgentSpawnData[] agents)
    {
        Agents = agents ?? Array.Empty<BattleAgentSpawnData>();
        RecordCount = Agents.Length;
        BatchCount = 1;
    }

    internal NetworkSpawnBattleAgents(
        byte[] payload,
        int uncompressedLength,
        int recordCount,
        bool isCompressed,
        Guid transferId,
        int batchIndex,
        int batchCount,
        SpawnBatchPurpose purpose,
        byte[] payloadSha256,
        BattleAgentSpawnData[] agents)
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

public enum SpawnBatchPurpose
{
    Initial = 0,
    Deployment = 1,
    CatchUp = 2,
}

/// <summary>One host-spawned battle agent: its network id, who it is, where it spawned, which side, and the
/// controller that owns it (the player whose hero it is, else the host for AI troops). The owner is the
/// authority that drives the agent's movement; the owning player also adopts its hero as its main agent.</summary>
[ProtoContract(SkipConstructor = true)]
public class BattleAgentSpawnData
{
    [ProtoMember(1)]
    public readonly Guid AgentId;
    [ProtoMember(2)]
    public readonly string CharacterId;
    [ProtoMember(4)]
    public readonly Vec3 Position;
    [ProtoMember(5)]
    public readonly BattleSideEnum Side;
    [ProtoMember(6)]
    public readonly float Health;
    [ProtoMember(7)]
    public readonly string OwnerControllerId;
    // Casualty attribution: the map-event party this troop belongs to and the unique troop descriptor seed
    // the server's MapEventParty.OnTroopKilled path keys on, so the owner can report the casualty on death.
    [ProtoMember(8)]
    public readonly string MapEventPartyId;
    [ProtoMember(9)]
    public readonly int TroopSeed;
    [ProtoMember(10)]
    public readonly Equipment SpawnEquipment;
    [ProtoMember(11)]
    public readonly BodyProperties BodyProperties;
    [ProtoMember(12)]
    public readonly MissionEquipmentData MissionEquipmentData;
    // Network id of this agent's MOUNT (Guid.Empty when unmounted). The engine spawns the horse implicitly
    // with the rider (from its equipment) on every client; carrying the owner's id for it lets the receiver
    // register its puppet's horse under the SAME identity, so mount hits/deaths sync by the horse's own id.
    [ProtoMember(13)]
    public readonly Guid MountAgentId;
    // The formation slot (a FormationClass cast to int, -1 for none) the owner placed this agent in, so a puppet
    // mirrors the owner's actual deployment split instead of a default troop-class grouping.
    [ProtoMember(14)]
    public readonly int FormationIndex;
    // Compact identities used only by the high-frequency movement stream. The canonical Guids remain
    // authoritative for reliable gameplay messages.
    [ProtoMember(15)]
    public readonly ushort MovementId;
    [ProtoMember(16)]
    public readonly ushort MountMovementId;
    [ProtoMember(17)]
    public readonly string OriginalOwnerControllerId;
    [ProtoMember(18)]
    public readonly AgentEquipmentData CurrentEquipment;
    [ProtoMember(19)]
    public readonly bool HasCurrentEquipment;
    [ProtoMember(20)]
    public readonly string MovementScopeId;
    [ProtoMember(21)]
    public readonly string MountOriginalOwnerControllerId;
    [ProtoMember(22)]
    public readonly string MountMovementScopeId;
    [ProtoMember(23)]
    public readonly bool IsRunningAway;
    [ProtoMember(24)]
    public readonly long AuthorityRevision;
    [ProtoMember(25)]
    public readonly long MountAuthorityRevision;

    public BattleAgentSpawnData(
        Guid agentId,
        string characterId,
        Vec3 position,
        BattleSideEnum side,
        float health,
        string ownerControllerId,
        string mapEventPartyId,
        int troopSeed,
        Equipment spawnEquipment,
        BodyProperties bodyProperties,
        MissionEquipmentData missionEquipmentData,
        Guid mountAgentId = default,
        int formationIndex = -1,
        ushort movementId = 0,
        ushort mountMovementId = 0,
        string originalOwnerControllerId = null,
        AgentEquipmentData? currentEquipment = null,
        string movementScopeId = null,
        string mountOriginalOwnerControllerId = null,
        string mountMovementScopeId = null,
        bool isRunningAway = false,
        long authorityRevision = 0,
        long mountAuthorityRevision = 0)
    {
        AgentId = agentId;
        CharacterId = characterId;
        Position = position;
        Side = side;
        Health = health;
        OwnerControllerId = ownerControllerId;
        MapEventPartyId = mapEventPartyId;
        TroopSeed = troopSeed;
        SpawnEquipment = spawnEquipment;
        BodyProperties = bodyProperties;
        MissionEquipmentData = missionEquipmentData;
        MountAgentId = mountAgentId;
        FormationIndex = formationIndex;
        MovementId = movementId;
        MountMovementId = mountMovementId;
        OriginalOwnerControllerId = originalOwnerControllerId ?? ownerControllerId;
        MovementScopeId = movementScopeId ?? OriginalOwnerControllerId;
        MountOriginalOwnerControllerId =
            mountOriginalOwnerControllerId ?? OriginalOwnerControllerId;
        MountMovementScopeId = mountMovementScopeId ?? MovementScopeId;
        CurrentEquipment = currentEquipment.GetValueOrDefault();
        HasCurrentEquipment = currentEquipment.HasValue;
        IsRunningAway = isRunningAway;
        AuthorityRevision = authorityRevision;
        MountAuthorityRevision = mountAuthorityRevision;
    }
}
