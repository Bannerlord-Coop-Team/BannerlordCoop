using Common.Messaging;
using Common.Network;
using Missions.Services.Agents.Messages;
using Missions.Services.Network;
using System;
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
        public WeaponDropHandler(INetworkAgentRegistry networkAgentRegistry, INetworkMessageBroker networkMessageBroker)
        {
            this.networkAgentRegistry = networkAgentRegistry;
            this.networkMessageBroker = networkMessageBroker;

            networkMessageBroker.Subscribe<WeaponDropped>(WeaponDropSend);
            networkMessageBroker.Subscribe<NetworkWeaponDropped>(WeaponDropRecieve);
        }

        ~WeaponDropHandler()
        {
            Dispose();
        }

        public void Dispose()
        {
            networkMessageBroker.Unsubscribe<WeaponDropped>(WeaponDropSend);
            networkMessageBroker.Unsubscribe<NetworkWeaponDropped>(WeaponDropRecieve);
        }

        private void WeaponDropSend(MessagePayload<WeaponDropped> obj)
        {
            if (!networkAgentRegistry.IsControlled(obj.What.Agent)) return;
            
            networkAgentRegistry.TryGetAgentId(obj.What.Agent, out Guid agentId);

            NetworkWeaponDropped message = new NetworkWeaponDropped(agentId, obj.What.EquipmentIndex);

            networkMessageBroker.PublishNetworkEvent(message);
        }

        private void WeaponDropRecieve(MessagePayload<NetworkWeaponDropped> obj)
        { 
            networkAgentRegistry.TryGetAgent(obj.What.AgentGuid, out Agent agent);

            agent.DropItem(obj.What.EquipmentIndex);
        }
    }
}
