using ProtoBuf;
using System;
using TaleWorlds.Library;

namespace Missions.Data;

/// <summary>
/// Data Class for AiAgent
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class CoopAgentSpawnData
{
    [ProtoMember(1)]
    public readonly Guid AgentId;
    [ProtoMember(2)]
    public readonly string CharacterObjectId;
    [ProtoMember(3)]
    public readonly Vec3 Position;
    [ProtoMember(4)]
    public readonly float Health;
    [ProtoMember(5)]
    public readonly bool IsPlayer;
    [ProtoMember(6)]
    public readonly bool HasMount;

    public CoopAgentSpawnData(
        Guid agentId,
        string characterObjectId,
        Vec3 position,
        float health,
        bool isPlayer,
        bool hasMount = false)
    {
        AgentId = agentId;
        CharacterObjectId = characterObjectId;
        Position = position;
        Health = health;
        IsPlayer = isPlayer;
        HasMount = hasMount;
    }
}
