using Common.Messaging;
using ProtoBuf;
using System;

namespace Missions.Agents.Messages;

/// <summary>
/// Replicates a player's battle-order voice event to the other mission clients.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkAgentVoicePlayed : IEvent
{
    [ProtoMember(1)]
    public Guid AgentId { get; }

    [ProtoMember(2)]
    public string VoiceTypeId { get; }

    public NetworkAgentVoicePlayed(Guid agentId, string voiceTypeId)
    {
        AgentId = agentId;
        VoiceTypeId = voiceTypeId;
    }
}
