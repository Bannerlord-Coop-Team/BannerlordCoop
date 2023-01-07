using Common.Messaging;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Network.Messages.Agents
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
