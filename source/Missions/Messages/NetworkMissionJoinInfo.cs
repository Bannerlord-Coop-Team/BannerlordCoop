using Common.Messaging;
using Missions.Data;
using ProtoBuf;
using System;

namespace Missions.Messages;

/// <summary>
/// External event for Join Info in Mission
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkMissionJoinInfo : IEvent
{
    [ProtoMember(1)]
    public readonly string ControllerId;
    [ProtoMember(2)]
    public readonly bool IsPlayerAlive;
    [ProtoMember(3)]
    public readonly CoopAgentSpawnData[] AiAgentData = Array.Empty<CoopAgentSpawnData>();

    public NetworkMissionJoinInfo(
        string controllerId,
        bool isPlayerAlive,
        CoopAgentSpawnData[] aiAgentDatas)
    {
        ControllerId = controllerId;
        IsPlayerAlive = isPlayerAlive;
        AiAgentData = aiAgentDatas;
    }
}
