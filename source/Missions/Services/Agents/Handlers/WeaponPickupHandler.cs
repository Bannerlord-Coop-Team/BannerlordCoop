using Common.Messaging;
using Common.Network;
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
        private static MethodInfo WeaponEquippedMethod = typeof(Agent).GetMethod("WeaponEquipped", BindingFlags.NonPublic | BindingFlags.Instance);

        public static void WeaponPickupSend(MessagePayload<WeaponPickupInternal> obj)
        {
            Agent agent = obj.Who as Agent;

            NetworkAgentRegistry.Instance.TryGetAgentId(agent, out Guid agentId);

            WeaponPickupExternal message = new WeaponPickupExternal(agentId, obj.What.EquipmentIndex, obj.What.WeaponObject, obj.What.WeaponModifier, obj.What.Banner);

            NetworkMessageBroker.Instance.PublishNetworkEvent(message);

        }
        public static void WeaponPickupReceive(MessagePayload<WeaponPickupExternal> obj)
        {
            //ItemObject - ItemModifier - Banner creates MissionWeapon
            MissionWeapon missionWeapon = new MissionWeapon(obj.What.ItemObject, obj.What.ItemModifier, obj.What.Banner);

            NetworkAgentRegistry.Instance.TryGetAgent(obj.What.AgentId, out Agent agent);
            
            agent.Equipment[obj.What.EquipmentIndex] = missionWeapon;
            WeaponEquippedMethod.Invoke(agent, new object[]
                {
                obj.What.EquipmentIndex,
                missionWeapon.GetWeaponData(true),
                missionWeapon.GetWeaponStatsData(),
                missionWeapon.GetAmmoWeaponData(true),
                missionWeapon.GetAmmoWeaponStatsData(),
                null,
                false,
                false
                });
        }
    }

    [HarmonyPatch(typeof(Agent), "EquipWeaponFromSpawnedItemEntity")]
    public class WeaponPickupHandlerPatch
    {
        static void Postfix(ref Agent __instance, EquipmentIndex slotIndex, SpawnedItemEntity spawnedItemEntity, bool removeWeapon)
        {
            WeaponPickupInternal message = new WeaponPickupInternal(__instance, slotIndex, spawnedItemEntity.WeaponCopy.Item, spawnedItemEntity.WeaponCopy.ItemModifier, spawnedItemEntity.WeaponCopy.Banner);

            MessageBroker.Instance.Publish(__instance, message);
        }
    }
}
