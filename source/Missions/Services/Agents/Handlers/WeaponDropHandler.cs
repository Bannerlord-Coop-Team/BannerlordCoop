using Common.Messaging;
using Common.Network;
using HarmonyLib;
using Missions.Services.Agents.Messages;
using Missions.Services.Agents.Patches;
using Missions.Services.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Handlers
{
    public class WeaponDropHandler
    {

        public static void WeaponDropSend(MessagePayload<WeaponDropInternal> obj)
        {
            NetworkAgentRegistry.Instance.TryGetAgentId(obj.What.Agent, out Guid agentId);

            WeaponDropExternal message = new WeaponDropExternal(agentId, obj.What.EquipmentIndex);

            NetworkMessageBroker.Instance.PublishNetworkEvent(message);
        }

        public static void WeaponDropRecieve(MessagePayload<WeaponDropExternal> obj)
        { 
            NetworkAgentRegistry.Instance.TryGetAgent(obj.What.AgentGuid, out Agent agent);

            agent.RemoveEquippedWeapon(obj.What.EquipmentIndex);
        }
    }

    [HarmonyPatch(typeof(Agent), "DropItem")]
    public class WeaponDropHandlerPatch
    {
        static void Postfix(ref Agent __instance, EquipmentIndex itemIndex, WeaponClass pickedUpItemType)
        {
            WeaponDropInternal message = new WeaponDropInternal(__instance, itemIndex);

            //Commented out as missiles are not functional yet
            MessageBroker.Instance.Publish(__instance, message);
        }
    }


}
