using Common.Messaging;
using ProtoBuf;
using System;
using TaleWorlds.Library;

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
    [ProtoMember(3)]
    public readonly bool IsHero;
    [ProtoMember(4)]
    public readonly Vec3 Position;
    [ProtoMember(5)]
    public readonly int Side;
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

    public BattleAgentSpawnData(Guid agentId, string characterId, bool isHero, Vec3 position, int side, float health, string ownerControllerId, string mapEventPartyId, int troopSeed)
    {
        AgentId = agentId;
        CharacterId = characterId;
        IsHero = isHero;
        Position = position;
        Side = side;
        Health = health;
        OwnerControllerId = ownerControllerId;
        MapEventPartyId = mapEventPartyId;
        TroopSeed = troopSeed;
    }
}
