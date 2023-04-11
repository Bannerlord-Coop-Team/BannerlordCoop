using Common.Messaging;
using Common.Network;
using Missions.Services.Agents.Messages;
using Missions.Services.Network;
using System;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Handlers
{
    internal interface IShieldBreakHandler : IHandler
    {
        void ShieldBreakSend(MessagePayload<ShieldBreak> payload);
        void ShieldBreakRecieve(MessagePayload<NetworkShieldBreak> payload);
    }
    public class ShieldBreakHandler : IShieldBreakHandler
    {
        readonly NetworkAgentRegistry networkAgentRegistry;
        readonly NetworkMessageBroker networkMessageBroker;
        public ShieldBreakHandler(NetworkAgentRegistry networkAgentRegistry, NetworkMessageBroker networkMessageBroker) 
        {
            this.networkAgentRegistry = networkAgentRegistry;
            this.networkMessageBroker = networkMessageBroker;

            networkMessageBroker.Subscribe<ShieldBreak>(ShieldBreakSend);
            networkMessageBroker.Subscribe<NetworkShieldBreak>(ShieldBreakRecieve);
        }
        ~ShieldBreakHandler()
        {
            networkMessageBroker.Unsubscribe<ShieldBreak>(ShieldBreakSend);
            networkMessageBroker.Unsubscribe<NetworkShieldBreak>(ShieldBreakRecieve);
        }

        public void ShieldBreakSend(MessagePayload<ShieldBreak> payload)
        {
            networkAgentRegistry.TryGetAgentId(payload.What.Agent, out Guid agentId);

            NetworkShieldBreak message = new NetworkShieldBreak(agentId, payload.What.EquipmentIndex);

            networkMessageBroker.PublishNetworkEvent(message);
        }

        public void ShieldBreakRecieve(MessagePayload<NetworkShieldBreak> payload)
        {
            networkAgentRegistry.TryGetAgent(payload.What.AgentGuid, out Agent agent);

            if (!agent.Equipment[payload.What.EquipmentIndex].IsEmpty)
            {
                agent.RemoveEquippedWeapon(payload.What.EquipmentIndex);
            }
        }
    }
}