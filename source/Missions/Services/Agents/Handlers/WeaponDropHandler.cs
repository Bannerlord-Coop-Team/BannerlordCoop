using Common.Logging;
using Common.Messaging;
using Common.Network;
using Missions.Services.Agents.Messages;
using Missions.Services.Network;
using Serilog;
using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Handlers
{
    /// <summary>
    /// Handler for weapon drops in a battle
    /// </summary>
    public interface IWeaponDropHandler : IHandler, IDisposable
    {

    }
    /// <inheritdoc/>
    public class WeaponDropHandler : IWeaponDropHandler
    {
        readonly INetworkAgentRegistry networkAgentRegistry;
        readonly INetworkMessageBroker networkMessageBroker;
        readonly static ILogger Logger = LogManager.GetLogger<WeaponDropHandler>();

        public WeaponDropHandler(INetworkAgentRegistry networkAgentRegistry, INetworkMessageBroker networkMessageBroker)
        {
            this.networkAgentRegistry = networkAgentRegistry;
            this.networkMessageBroker = networkMessageBroker;

            networkMessageBroker.Subscribe<WeaponDropped>(WeaponDropSend);
            networkMessageBroker.Subscribe<NetworkWeaponDropped>(WeaponDropReceive);
        }

        ~WeaponDropHandler()
        {
            Dispose();
        }

        public void Dispose()
        {
            networkMessageBroker.Unsubscribe<WeaponDropped>(WeaponDropSend);
            networkMessageBroker.Unsubscribe<NetworkWeaponDropped>(WeaponDropReceive);
        }

        private void WeaponDropSend(MessagePayload<WeaponDropped> obj)
        {
            if (networkAgentRegistry.IsControlled(obj.What.Agent) == false) return;
            
            if(networkAgentRegistry.TryGetAgentId(obj.What.Agent, out Guid agentId) == false)
            {
                Logger.Warning("No agentID was found for the Agent: {agent} in {class}", obj.What.Agent, typeof(WeaponDropHandler));
                return;
            }

            NetworkWeaponDropped message = new NetworkWeaponDropped(agentId, obj.What.EquipmentIndex);

            networkMessageBroker.PublishNetworkEvent(message);
        }

        private void WeaponDropReceive(MessagePayload<NetworkWeaponDropped> obj)
        { 
            if(networkAgentRegistry.TryGetAgent(obj.What.AgentGuid, out Agent agent) == false)
            {
                Logger.Warning("No agent found for {guid} in {class}", obj.What.AgentGuid, typeof(WeaponDropHandler));
                return;
            }

            if (agent.GetWeaponEntityFromEquipmentSlot(obj.What.EquipmentIndex) == null)
            {
                Logger.Error($"Tried to drop a weapon from an empty slot ({obj.What.EquipmentIndex})");
                return;
            }

            agent.DropItem(obj.What.EquipmentIndex);
        }
    }
}
