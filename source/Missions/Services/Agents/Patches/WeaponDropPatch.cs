using Common.Messaging;
using HarmonyLib;
using Missions.Services.Agents.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Patches
{
    public class WeaponDropPatch
    {

        [HarmonyPatch(typeof(Agent), "DropItem")]
        public class WeaponDropHandlerPatch
        {
            static void Postfix(ref Agent __instance, EquipmentIndex itemIndex, WeaponClass pickedUpItemType)
            {
                WeaponDropped message = new WeaponDropped(__instance, itemIndex);

                //Commented out as missiles are not functional yet
                MessageBroker.Instance.Publish(__instance, message);
            }
        }
    }
}
