using Common.Messaging;
using TaleWorlds.MountAndBlade;

namespace Missions.Messages.Agents
{
    public readonly struct AgentInteraction : IEvent
    {
        public Agent reqAgent { get; }
        public Agent tarAgent { get; }

        public AgentInteraction(Agent req, Agent tar)
        {
            reqAgent = req;
            tarAgent = tar;
        }
    }
}