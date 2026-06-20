using Common.Messaging;
using Missions.Agents.Messages;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Handlers
{
    /// <summary>
    /// Handler for agent death
    /// </summary>
    public interface IAgentDeathHandler : IHandler
    {

    }
    
    /// <inheritdoc/>
    public class AgentDeathHandler : IAgentDeathHandler
    {
        private readonly INetworkAgentRegistry agentRegistry;
        private readonly IMessageBroker messageBroker;
        
        public AgentDeathHandler(
            INetworkAgentRegistry agentRegistry,
            IMessageBroker messageBroker)
        {
            this.agentRegistry = agentRegistry;
            this.messageBroker = messageBroker;

            this.messageBroker.Subscribe<AgentDied>(Handle);
        }
        
        ~AgentDeathHandler()
        {
            Dispose();
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<AgentDied>(Handle);
        }

        private void Handle(MessagePayload<AgentDied> obj)
        {
            Agent agent = obj.What.Agent;
            if (agentRegistry.TryGetAgentInfo(agent, out var agentInfo))
            {
                agentRegistry.RemoveAgent(agentInfo.AgentId);
            }
        }
    }
}