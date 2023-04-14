using Common.Logging;
using Common.Messaging;
using Common.Network;
using Missions.Services.Agents.Messages;
using Missions.Services.Network;
using Serilog;
using Serilog.Core;
using System;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Handlers
{
    /// <summary>
    /// Handler for shield breaks in a battle
    /// </summary>
    public interface IShieldDamageHandler : IHandler, IDisposable
    {

    }
    /// <inheritdoc/>
    public class ShieldDamageHandler : IShieldDamageHandler
    {
        readonly INetworkAgentRegistry networkAgentRegistry;
        readonly INetworkMessageBroker networkMessageBroker;
        readonly static ILogger Logger = LogManager.GetLogger<ShieldDamageHandler>();

        public ShieldDamageHandler(INetworkAgentRegistry networkAgentRegistry, INetworkMessageBroker networkMessageBroker) 
        {
            this.networkAgentRegistry = networkAgentRegistry;
            this.networkMessageBroker = networkMessageBroker;

            networkMessageBroker.Subscribe<ShieldDamaged>(ShieldBreakSend);
            networkMessageBroker.Subscribe<NetworkShieldBreak>(ShieldBreakRecieve);
        }
        ~ShieldDamageHandler()
        {
            Dispose();
        }

        public void Dispose()
        {
            networkMessageBroker.Unsubscribe<ShieldDamaged>(ShieldBreakSend);
            networkMessageBroker.Unsubscribe<NetworkShieldBreak>(ShieldBreakRecieve);
        }

        private void ShieldBreakSend(MessagePayload<ShieldDamaged> payload)
        {
            if (payload.What.Hitpoints > 0) return;

            if (networkAgentRegistry.TryGetAgentId(payload.What.Agent, out Guid agentId) == false) return;

            if (networkAgentRegistry.IsControlled(agentId) == false) return;

            NetworkShieldBreak message = new NetworkShieldBreak(agentId, payload.What.EquipmentIndex);

            networkMessageBroker.PublishNetworkEvent(message);
        }

        private void ShieldBreakRecieve(MessagePayload<NetworkShieldBreak> payload)
        {
            if (networkAgentRegistry.TryGetAgent(payload.What.AgentGuid, out Agent agent) == false)
            {
                Logger.Warning("No agent found at {guid} in {class}", payload.What.AgentGuid, typeof(ShieldDamageHandler));
                return;
            }

            if (agent.Equipment[payload.What.EquipmentIndex].IsEmpty)
            {
                Logger.Warning("Equipment Index for {agent} is already empty in {class}", agent, typeof(ShieldDamageHandler));
                return;
            }

            agent.RemoveEquippedWeapon(payload.What.EquipmentIndex);
        }
    }
}