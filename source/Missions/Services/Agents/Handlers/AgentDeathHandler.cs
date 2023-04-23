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
        private readonly INetworkMessageBroker networkMessageBroker;
        
        public AgentDeathHandler(
            INetworkAgentRegistry agentRegistry,
            INetworkMessageBroker networkMessageBroker)
        {
            this.agentRegistry = agentRegistry;
            this.networkMessageBroker = networkMessageBroker;

            this.networkMessageBroker.Subscribe<AgentDied>(Handle);
        }
        
        ~AgentDeathHandler()
        {
            Dispose();
        }

        public void Dispose()
        {
            networkMessageBroker.Unsubscribe<AgentDied>(Handle);
        }

        private void Handle(MessagePayload<AgentDied> obj)
        {
            Agent agent = obj.What.Agent;
            if (agentRegistry.TryGetAgentId(agent, out Guid agentId))
            {
                // TODO find a way to keep alive for any non-processed damage
                //agentRegistry.RemoveControlledAgent(agentId);
                //agentRegistry.RemoveNetworkControlledAgent(agentId);
            }
        }
    }
}