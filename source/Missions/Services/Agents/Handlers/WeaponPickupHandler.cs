using Autofac;
using Common.Messaging;
using Common.Network;
using GameInterface.Serialization;
using HarmonyLib;
using Missions.Services.Agents.Messages;
using Missions.Services.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Handlers
{
    /// <summary>
    /// Handler for weapon pickups within a battle
    /// </summary>
    public interface IWeaponPickupHandler : IHandler, IDisposable
    {

    }
    /// <inheritdoc/>
    public class WeaponPickupHandler : IWeaponPickupHandler
    {
        readonly INetworkAgentRegistry networkAgentRegistry;
        readonly INetworkMessageBroker networkMessageBroker;
        public WeaponPickupHandler(INetworkAgentRegistry networkAgentRegistry, INetworkMessageBroker networkMessageBroker)
        {
            this.networkAgentRegistry = networkAgentRegistry;
            this.networkMessageBroker = networkMessageBroker;

            networkMessageBroker.Subscribe<WeaponPickedup>(WeaponPickupSend);
            networkMessageBroker.Subscribe<NetworkWeaponPickedup>(WeaponPickupReceive);

        }
        ~WeaponPickupHandler()
        {
            Dispose();
        }

        public void Dispose()
        {
            networkMessageBroker.Unsubscribe<WeaponPickedup>(WeaponPickupSend);
            networkMessageBroker.Unsubscribe<NetworkWeaponPickedup>(WeaponPickupReceive);
        }

        private void WeaponPickupSend(MessagePayload<WeaponPickedup> obj)
        {
            if (!networkAgentRegistry.IsControlled(obj.What.Agent)) return;

            networkAgentRegistry.TryGetAgentId(obj.What.Agent, out Guid agentId);

            NetworkWeaponPickedup message = new NetworkWeaponPickedup(
                agentId, 
                obj.What.EquipmentIndex, 
                obj.What.WeaponObject, 
                obj.What.WeaponModifier, 
                obj.What.Banner);

            networkMessageBroker.PublishNetworkEvent(message);
        }
        private void WeaponPickupReceive(MessagePayload<NetworkWeaponPickedup> obj)
        {
            //ItemObject - ItemModifier - Banner creates MissionWeapon
            MissionWeapon missionWeapon = new MissionWeapon(obj.What.ItemObject, obj.What.ItemModifier, obj.What.Banner);

            networkAgentRegistry.TryGetAgent(obj.What.AgentId, out Agent agent);

            agent.EquipWeaponWithNewEntity(obj.What.EquipmentIndex, ref missionWeapon);
        }
    }
}
