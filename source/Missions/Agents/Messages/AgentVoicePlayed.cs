using Common.Messaging;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Messages;

/// <summary>
/// Offers a local order voice to the mission handler before vanilla chooses a recording.
/// </summary>
public sealed class AgentVoicePlayed : IEvent
{
    public Agent Agent { get; }
    public string VoiceTypeId { get; }
    public bool Handled { get; set; }

    public AgentVoicePlayed(Agent agent, string voiceTypeId)
    {
        Agent = agent;
        VoiceTypeId = voiceTypeId;
    }
}
