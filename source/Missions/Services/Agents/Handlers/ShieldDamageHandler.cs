using Common.Messaging;
using Common.Network;
using Missions.Services.Agents.Messages;
using Missions.Services.Network;
using System;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Handlers
{
    /// <summary>
    /// Handler for shield breaks in a battle
    /// </summary>
    public interface IShieldDamageHandler : IHandler
    {

    }
    /// <inheritdoc/>
    public class ShieldDamageHandler : IShieldDamageHandler
    {
        readonly NetworkAgentRegistry networkAgentRegistry;
        readonly NetworkMessageBroker networkMessageBroker;
        public ShieldDamageHandler(NetworkAgentRegistry networkAgentRegistry, NetworkMessageBroker networkMessageBroker) 
        {
            this.networkAgentRegistry = networkAgentRegistry;
            this.networkMessageBroker = networkMessageBroker;

            networkMessageBroker.Subscribe<ShieldHealthRemaining>(ShieldBreakSend);
            networkMessageBroker.Subscribe<NetworkShieldBreak>(ShieldBreakRecieve);
        }
        ~ShieldDamageHandler()
        {
            networkMessageBroker.Unsubscribe<ShieldHealthRemaining>(ShieldBreakSend);
            networkMessageBroker.Unsubscribe<NetworkShieldBreak>(ShieldBreakRecieve);
        }

        private void ShieldBreakSend(MessagePayload<ShieldHealthRemaining> payload)
        {
            if (payload.What.Hitpoints > 0) return;

            if (networkAgentRegistry.TryGetAgentId(payload.What.Agent, out Guid agentId) == false) return;

            if (networkAgentRegistry.IsControlled(agentId) == false) return;

            NetworkShieldBreak message = new NetworkShieldBreak(agentId, payload.What.EquipmentIndex);

            networkMessageBroker.PublishNetworkEvent(message);
        }

        private void ShieldBreakRecieve(MessagePayload<NetworkShieldBreak> payload)
        {
            networkAgentRegistry.TryGetAgent(payload.What.AgentGuid, out Agent agent);

            if (!agent.Equipment[payload.What.EquipmentIndex].IsEmpty)
            {
                agent.RemoveEquippedWeapon(payload.What.EquipmentIndex);
            }
        }
    }
}