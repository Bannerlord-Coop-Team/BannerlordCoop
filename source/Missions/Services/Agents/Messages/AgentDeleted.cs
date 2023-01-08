using Common.Messaging;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Messages
{
    public readonly struct AgentDeleted : IEvent
    {
        public Agent Agent { get; }

        public AgentDeleted(Agent agent)
        {
            Agent = agent;
        }
    }
}
