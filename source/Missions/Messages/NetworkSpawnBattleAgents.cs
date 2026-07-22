using Common.Messaging;
using Missions.Data;
using ProtoBuf;
using System;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Messages;

/// <summary>
/// Host → peers (over the mission mesh): a batch of agents the host spawned into the battle, for peers to
/// recreate as puppets. v1 puppets are inert — movement, combat, death and control/authority sync are
/// layered on in Phase 3.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkSpawnBattleAgents : IEvent
{
    [ProtoMember(1)]
    public readonly BattleAgentSpawnData[] Agents = Array.Empty<BattleAgentSpawnData>();

    public NetworkSpawnBattleAgents(BattleAgentSpawnData[] agents)
    {
        Agents = agents;
    }
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
        int formationIndex = -1)
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
    }
}
