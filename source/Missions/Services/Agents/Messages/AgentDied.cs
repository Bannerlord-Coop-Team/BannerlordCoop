using Common.Messaging;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Patches
{
    /// <summary>
    /// Internal event for Agent deaths
    /// </summary>
    internal readonly struct AgentDied : IEvent
    {
        public Agent Agent { get; }

        public AgentDied(Agent agent)
        {
            Agent = agent;
        }
    }
}