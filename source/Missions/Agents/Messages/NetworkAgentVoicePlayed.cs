using Common.Messaging;
using ProtoBuf;
using System;

namespace Missions.Agents.Messages;

/// <summary>
/// Replicates a player's battle-order voice and exact vanilla recording to the other mission clients.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkAgentVoicePlayed : IEvent
{
    [ProtoMember(1)]
    public Guid AgentId { get; }

    [ProtoMember(2)]
    public string VoiceTypeId { get; }

    [ProtoMember(3)]
    public string SampleName { get; }

    public NetworkAgentVoicePlayed(Guid agentId, string voiceTypeId, string sampleName)
    {
        AgentId = agentId;
        VoiceTypeId = voiceTypeId;
        SampleName = sampleName;
    }
}
