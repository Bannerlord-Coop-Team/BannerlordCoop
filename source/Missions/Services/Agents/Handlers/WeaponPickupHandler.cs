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
    public class WeaponPickupHandler
    {

        public WeaponPickupHandler()
        {
            ContainerBuilder builder = new ContainerBuilder();
            builder.RegisterModule<MissionModule>();
            container = builder.Build();

            binaryPackageFactory = container.Resolve<IBinaryPackageFactory>();

            NetworkMessageBroker.Instance.Subscribe<WeaponPickedup>(WeaponPickupSend);
            NetworkMessageBroker.Instance.Subscribe<NetworkWeaponPickedup>(WeaponPickupReceive);
        }
        ~WeaponPickupHandler()
        {
            NetworkMessageBroker.Instance.Unsubscribe<WeaponPickedup>(WeaponPickupSend);
            NetworkMessageBroker.Instance.Unsubscribe<NetworkWeaponPickedup>(WeaponPickupReceive);
        }

        private static MethodInfo WeaponEquippedMethod = typeof(Agent).GetMethod("WeaponEquipped", BindingFlags.NonPublic | BindingFlags.Instance);
        private IContainer container;
        private IBinaryPackageFactory binaryPackageFactory;

        public void WeaponPickupSend(MessagePayload<WeaponPickedup> obj)
        {
            Agent agent = obj.Who as Agent;

            NetworkAgentRegistry.Instance.TryGetAgentId(agent, out Guid agentId);

            NetworkWeaponPickedup message = new NetworkWeaponPickedup(binaryPackageFactory, agentId, obj.What.EquipmentIndex, obj.What.WeaponObject, obj.What.WeaponModifier, obj.What.Banner);

            NetworkMessageBroker.Instance.PublishNetworkEvent(message);
        }
        public void WeaponPickupReceive(MessagePayload<NetworkWeaponPickedup> obj)
        {
            //ItemObject - ItemModifier - Banner creates MissionWeapon
            MissionWeapon missionWeapon = new MissionWeapon(obj.What.ItemObject, obj.What.ItemModifier, obj.What.Banner);

            NetworkAgentRegistry.Instance.TryGetAgent(obj.What.AgentId, out Agent agent);

            agent.EquipWeaponWithNewEntity(obj.What.EquipmentIndex, ref missionWeapon);
        }
    }
}
