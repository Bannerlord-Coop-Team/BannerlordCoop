using Common.Messaging;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Messages;

/// <summary>
/// A voice event emitted by a local mission agent.
/// </summary>
public readonly struct AgentVoicePlayed : IEvent
{
    public Agent Agent { get; }
    public string VoiceTypeId { get; }

    public AgentVoicePlayed(Agent agent, string voiceTypeId)
    {
        Agent = agent;
        VoiceTypeId = voiceTypeId;
    }
}
