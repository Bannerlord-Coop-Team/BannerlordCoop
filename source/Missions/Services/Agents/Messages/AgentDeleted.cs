using Common.Messaging;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Messages
{
    /// <summary>
    /// External event for Agent deletion
    /// </summary>
    public readonly struct AgentDeleted : IEvent
    {
        public Agent Agent { get; }

        public AgentDeleted(Agent agent)
        {
            Agent = agent;
        }
    }
}
