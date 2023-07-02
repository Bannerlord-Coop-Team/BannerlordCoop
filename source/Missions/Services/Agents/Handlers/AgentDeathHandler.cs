using System;
using Common.Messaging;
using Common.Network;
using Missions.Services.Agents.Patches;
using Missions.Services.Network;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Handlers
{
    /// <summary>
    /// Handler for agent death
    /// </summary>
    public interface IAgentDeathHandler : IHandler, IDisposable
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
            if (agentRegistry.TryGetAgentId(agent, out Guid agentId))
            {
                agentRegistry.RemoveControlledAgent(agentId);
                agentRegistry.RemoveNetworkControlledAgent(agentId);
            }
        }
    }
}