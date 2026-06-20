using Common.Messaging;
using HarmonyLib;
using Missions.Agents.Messages;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Patches
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
