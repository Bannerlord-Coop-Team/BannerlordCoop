using Common.Messaging;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Messages;

/// <summary>A locally authoritative collision that Bannerlord resolved as guarded.</summary>
public sealed class LocalAgentGuardedHit : IEvent
{
    public Agent AffectedAgent { get; }
    public Agent AffectorAgent { get; }

    public LocalAgentGuardedHit(Agent affectedAgent, Agent affectorAgent)
    {
        AffectedAgent = affectedAgent;
        AffectorAgent = affectorAgent;
    }
}
