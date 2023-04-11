using Common.Messaging;
using Common.Network;
using Missions.Services.Agents.Messages;
using Missions.Services.Network;
using System;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Handlers
{
    internal interface IWeaponDropHandler : IHandler
    {
        void WeaponDropSend(MessagePayload<WeaponDropped> obj);
        void WeaponDropRecieve(MessagePayload<NetworkWeaponDropped> obj);
    }
    public class WeaponDropHandler : IWeaponDropHandler
    {
        readonly NetworkAgentRegistry networkAgentRegistry;
        readonly NetworkMessageBroker networkMessageBroker;
        public WeaponDropHandler(NetworkAgentRegistry networkAgentRegistry, NetworkMessageBroker networkMessageBroker)
        {
            this.networkAgentRegistry = networkAgentRegistry;
            this.networkMessageBroker = networkMessageBroker;

            networkMessageBroker.Subscribe<WeaponDropped>(WeaponDropSend);
            networkMessageBroker.Subscribe<NetworkWeaponDropped>(WeaponDropRecieve);
        }

        ~WeaponDropHandler()
        {
            networkMessageBroker.Unsubscribe<WeaponDropped>(WeaponDropSend);
            networkMessageBroker.Unsubscribe<NetworkWeaponDropped>(WeaponDropRecieve);
        }

        public void WeaponDropSend(MessagePayload<WeaponDropped> obj)
        {
            networkAgentRegistry.TryGetAgentId(obj.What.Agent, out Guid agentId);

            NetworkWeaponDropped message = new NetworkWeaponDropped(agentId, obj.What.EquipmentIndex);

            networkMessageBroker.PublishNetworkEvent(message);
        }

        public void WeaponDropRecieve(MessagePayload<NetworkWeaponDropped> obj)
        { 
            networkAgentRegistry.TryGetAgent(obj.What.AgentGuid, out Agent agent);

            agent.DropItem(obj.What.EquipmentIndex);
        }
    }
}
