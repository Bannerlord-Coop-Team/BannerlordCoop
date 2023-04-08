using Common.Messaging;
using Common.Network;
using Missions.Services.Agents.Messages;
using Missions.Services.Network;
using System;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Handlers
{
    public class ShieldBreakHandler
    {
        public ShieldBreakHandler() 
        {
            NetworkMessageBroker.Instance.Subscribe<ShieldBreak>(ShieldBreakSend);
            NetworkMessageBroker.Instance.Subscribe<NetworkShieldBreak>(ShieldBreakRecieve);
        }
        ~ShieldBreakHandler()
        {
            NetworkMessageBroker.Instance.Unsubscribe<ShieldBreak>(ShieldBreakSend);
            NetworkMessageBroker.Instance.Unsubscribe<NetworkShieldBreak>(ShieldBreakRecieve);
        }

        public void ShieldBreakSend(MessagePayload<ShieldBreak> payload)
        {
            NetworkAgentRegistry.Instance.TryGetAgentId(payload.What.Agent, out Guid agentId);

            NetworkShieldBreak message = new NetworkShieldBreak(agentId, payload.What.EquipmentIndex);

            NetworkMessageBroker.Instance.PublishNetworkEvent(message);
        }

        public void ShieldBreakRecieve(MessagePayload<NetworkShieldBreak> payload)
        {
            NetworkAgentRegistry.Instance.TryGetAgent(payload.What.AgentGuid, out Agent agent);

            if (!agent.Equipment[payload.What.EquipmentIndex].IsEmpty)
            {
                agent.RemoveEquippedWeapon(payload.What.EquipmentIndex);
            }
        }
    }
}