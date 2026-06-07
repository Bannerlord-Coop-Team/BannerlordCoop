using Autofac;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Serialization;
using HarmonyLib;
using Missions.Services.Agents.Messages;
using Missions.Services.Network;
using Serilog;
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
        readonly INetwork network;
        readonly IMessageBroker messageBroker;
        readonly static ILogger Logger = LogManager.GetLogger<WeaponPickupHandler>();
        public WeaponPickupHandler(
            INetworkAgentRegistry networkAgentRegistry,
            INetwork network,
            IMessageBroker messageBroker)
        {
            this.networkAgentRegistry = networkAgentRegistry;
            this.network = network;
            this.messageBroker = messageBroker;

            messageBroker.Subscribe<WeaponPickedup>(WeaponPickupSend);
            messageBroker.Subscribe<NetworkWeaponPickedup>(WeaponPickupReceive);

        }
        ~WeaponPickupHandler()
        {
            Dispose();
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<WeaponPickedup>(WeaponPickupSend);
            messageBroker.Unsubscribe<NetworkWeaponPickedup>(WeaponPickupReceive);
        }

        private void WeaponPickupSend(MessagePayload<WeaponPickedup> obj)
        {
            var payload = obj.What;

            if (networkAgentRegistry.IsControlled(payload.Agent) == false) return;

            if(networkAgentRegistry.TryGetAgentId(payload.Agent, out Guid agentId) == false)
            {
                Logger.Warning("No agentID was found for the Agent: {agent} in {class}", payload.Agent, typeof(WeaponPickupHandler));
                return;
            }

            NetworkWeaponPickedup message = new NetworkWeaponPickedup(
                agentId,
                payload.EquipmentIndex,
                payload.WeaponObject,
                payload.WeaponModifier,
                payload.Banner);

            network.SendAll(message);
        }
        private void WeaponPickupReceive(MessagePayload<NetworkWeaponPickedup> obj)
        {
            //ItemObject - ItemModifier - Banner creates MissionWeapon
            MissionWeapon missionWeapon = new MissionWeapon(obj.What.ItemObject, obj.What.ItemModifier, obj.What.Banner);

            if (networkAgentRegistry.TryGetAgent(obj.What.AgentId, out Agent agent) == false)
            {
                Logger.Warning("No agent found at {guid} in {class}", obj.What.AgentId, typeof(WeaponPickupHandler));
                return;
            }

            if (obj.What.EquipmentIndex == EquipmentIndex.ExtraWeaponSlot)
            {
                agent.EquipWeaponToExtraSlotAndWield(ref missionWeapon);
                return;
            }

            agent.EquipWeaponWithNewEntity(obj.What.EquipmentIndex, ref missionWeapon);
        }
    }
}
