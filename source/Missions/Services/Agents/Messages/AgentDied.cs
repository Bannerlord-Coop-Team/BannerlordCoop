using Common.Messaging;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Patches
{
    internal readonly struct AgentDied : IEvent
    {
        public Agent Agent { get; }

        public AgentDied(Agent agent)
        {
            Agent = agent;
        }
    }
}