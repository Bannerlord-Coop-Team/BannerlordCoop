using ProtoBuf;
using System;
using TaleWorlds.Library;

namespace GameInterface.Missions.Services.Network.Data;

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

    public CoopAgentSpawnData(Guid agentId, string characterObjectId, Vec3 position, float health, bool isPlayer)
    {
        AgentId = agentId;
        CharacterObjectId = characterObjectId;
        Position = position;
        Health = health;
    }
}