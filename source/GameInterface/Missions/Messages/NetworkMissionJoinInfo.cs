using Common.Messaging;
using GameInterface.Missions.Services.Network.Data;
using ProtoBuf;
using System;
using TaleWorlds.Library;

namespace GameInterface.Missions.Services.Network.Messages;

/// <summary>
/// External event for Join Info in Mission
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkMissionJoinInfo : IEvent
{
    [ProtoMember(1)]
    public readonly string CharacterObjectId;
    [ProtoMember(2)]
    public readonly string ControllerId;
    [ProtoMember(3)]
    public readonly Vec3 StartingPosition;
    [ProtoMember(4)]
    public readonly bool IsPlayerAlive;
    [ProtoMember(5)]
    public readonly float PlayerHealth;
    [ProtoMember(6)]
    public readonly AiAgentData[] AiAgentData = Array.Empty<AiAgentData>();

    public NetworkMissionJoinInfo(
        string characterObjectId,
        bool isPlayerAlive,
        string controllerId,
        Vec3 startingPosition,
        float health,
        AiAgentData[] aiAgentDatas)
    {
        CharacterObjectId = characterObjectId;
        ControllerId = controllerId;
        StartingPosition = startingPosition;
        IsPlayerAlive = isPlayerAlive;
        PlayerHealth = health;
        AiAgentData = aiAgentDatas;
    }
}